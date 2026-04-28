using System;
using System.Collections.Generic;
using System.Linq;
using Constructables;
using Structures.Resources;
using UtilityLibrary;
#if DEBUG
using UI.Debug.Console;
#endif

namespace Structures.GameState;

/// <summary>
/// Manages the runtime economy for a single continent: stockpiles, production/consumption rates,
/// power generation/storage, building registration, and discrete manufacturing queue.
/// This is a plain C# class (not a Node) ticked by BodyEconomyManager each physics frame.
/// </summary>
public class ContinentEconomy : IResourceEndpoint
{
    private const float DEFAULT_CATEGORY_CAPACITY = 1000f;
    private const float DEFAULT_POWER_STORAGE_CAPACITY = 500f;
    private const string POWER_RESOURCE_ID = "power";

    private readonly Continent _continent;
    private readonly List<BuildingRegistration> _activeBuildings = new();
    private int _nextBuildingInstanceId;

    // Stockpile
    private readonly Dictionary<string, float> _stockpile = new();
    private readonly Dictionary<string, float> _categoryCapacity = new();
    private readonly Dictionary<string, List<(BuildingConstruction Building, float Capacity)>> _storageBuildingsByCategory = new();

    // Request Queue
    private readonly List<ResourceRequest> _requestQueue = new();
    private bool _queueDirty = false;

    // Rates (Theoretical maximums for UI)
    private readonly Dictionary<string, float> _productionRates = new();
    private readonly Dictionary<string, float> _consumptionRates = new();
    private readonly Dictionary<string, float> _netRates = new();
    private bool _ratesDirty = true;

    // Power
    private float _powerGeneration;
    private float _powerConsumption;
    private float _powerStored;
    private float _powerStorageCapacity = DEFAULT_POWER_STORAGE_CAPACITY;
    private bool _isPowerDeficit;

#if DEBUG
    private string? _debugNamespace;
#endif

    public Continent Continent => _continent;
    public bool IsPowerDeficit => _isPowerDeficit;
    public float PowerStored => _powerStored;
    public float PowerStorageCapacity => _powerStorageCapacity;
    public float PowerGeneration => _powerGeneration;
    public float PowerConsumption => _powerConsumption;
    public int ActiveBuildingCount => _activeBuildings.Count;
    public IReadOnlyList<BuildingRegistration> ActiveBuildings => _activeBuildings;

    public ContinentEconomy(Continent continent)
    {
        _continent = continent;
        InitializeDefaultCapacities();

#if DEBUG
        try
        {
            _debugNamespace = InstanceRegistry.RegisterContinentEconomy(this, continent.StartingIndex);
            GameLogger.Debug($"[ContinentEconomy] Registered with debug console as '{_debugNamespace}'");
            SignalBus.Instance?.EmitEconomyRegistered(_debugNamespace, "ContinentEconomy", continent.StartingIndex.ToString());
        }
        catch (Exception e)
        {
            GameLogger.Warning($"[ContinentEconomy] Failed to register with debug: {e.Message}");
        }
#endif
    }

    private void InitializeDefaultCapacities()
    {
        _categoryCapacity["ore"] = DEFAULT_CATEGORY_CAPACITY;
        _categoryCapacity["raw_material"] = DEFAULT_CATEGORY_CAPACITY;
        _categoryCapacity["fuel"] = DEFAULT_CATEGORY_CAPACITY;
        _categoryCapacity["food"] = DEFAULT_CATEGORY_CAPACITY;
        _categoryCapacity["construction"] = DEFAULT_CATEGORY_CAPACITY;
        _categoryCapacity["industrial"] = DEFAULT_CATEGORY_CAPACITY;
        _categoryCapacity["power"] = DEFAULT_POWER_STORAGE_CAPACITY;
    }

    public void RegisterBuilding(BuildingConstruction building, string recipeId)
    {
        if (building == null || string.IsNullOrEmpty(recipeId))
        {
            GameLogger.Warning("[ContinentEconomy] Cannot register building: null building or empty recipe");
            return;
        }

        var recipeDb = RecipeDatabase.Instance;
        if (recipeDb == null || !recipeDb.IsLoaded || !recipeDb.TryGetRecipe(recipeId, out var recipe) || recipe == null)
        {
            GameLogger.Warning($"[ContinentEconomy] Recipe '{recipeId}' not found, skipping registration for {building?.Name}");
            return;
        }

        // Add storage capacity if this building provides any
        if (building.Definition?.StartingStorageCapacity != null)
        {
            foreach (var kvp in building.Definition.StartingStorageCapacity)
            {
                AddStorageCapacity(kvp.Key, kvp.Value, building);
            }
        }

        float productionSpeed = building.Definition?.Production?.ProductionSpeed ?? 1.0f;
        float cyclesPerSecond = productionSpeed / recipe.WorkRequired;
        float depositYield = GetDepositYieldMultiplier(building, recipe);

        var registration = new BuildingRegistration
        {
            BuildingInstanceId = _nextBuildingInstanceId++,
            BuildingNode = building,
            RecipeId = recipeId,
            DepositYieldMultiplier = depositYield,
            ProductionSpeed = productionSpeed
        };

        // Compute theoretical input rates for UI
        foreach (var input in recipe.InputResources)
        {
            float rate = cyclesPerSecond * input.Value * depositYield;
            if (input.Key == POWER_RESOURCE_ID) registration.TheoreticalPowerConsumption = rate;
            else registration.TheoreticalInputRates[input.Key] = rate;
        }

        // Compute theoretical output rates for UI
        foreach (var output in recipe.OutputResources)
        {
            float rate = cyclesPerSecond * output.Value * depositYield;
            if (output.Key == POWER_RESOURCE_ID) registration.TheoreticalPowerGeneration = rate;
            else registration.TheoreticalOutputRates[output.Key] = rate;
        }

        _activeBuildings.Add(registration);
        _ratesDirty = true;
        
        GameLogger.Info($"[ContinentEconomy] Registered building '{building.Name}' with recipe '{recipeId}'");
    }

    public void UnregisterBuilding(BuildingConstruction building)
    {
        // Remove storage capacity
        if (building.Definition?.StartingStorageCapacity != null)
        {
            foreach (var kvp in building.Definition.StartingStorageCapacity)
            {
                RemoveStorageCapacity(kvp.Key, kvp.Value, building);
            }
        }
        
        // Remove pending requests
        _requestQueue.RemoveAll(r => r.Building == building);

        int removed = _activeBuildings.RemoveAll(r => r.BuildingNode == building);
        if (removed > 0)
        {
            _ratesDirty = true;
            GameLogger.Info($"[ContinentEconomy] Unregistered building '{building.Name}'");
        }
    }

    public bool ChangeRecipe(BuildingConstruction building, string newRecipeId)
    {
        if (!string.IsNullOrEmpty(building.Definition?.AllowedRecipeCategory))
        {
            if (!RecipeDatabase.Instance.TryGetRecipe(newRecipeId, out var recipe) || recipe == null) return false;
            if (recipe.Category != building.Definition.AllowedRecipeCategory) return false;
        }

        UnregisterBuilding(building);
        RegisterBuilding(building, newRecipeId);
        return true;
    }

    public void EnqueueResourceRequest(ResourceRequest request)
    {
        _requestQueue.Add(request);
        _queueDirty = true;
    }

    public void Tick(float delta, double totalTime)
    {
        if (_ratesDirty)
        {
            RecomputeTheoreticalRates();
            _ratesDirty = false;
        }

        if (_queueDirty)
        {
            ProcessQueue();
        }

        TickPower(delta);

        // Tick discrete manufacturing for all buildings
        foreach (var reg in _activeBuildings)
        {
            var mfg = reg.BuildingNode.Manufacturing;
            if (mfg.State == Enums.ManufacturingState.Idle)
            {
                if (RecipeDatabase.Instance.TryGetRecipe(reg.RecipeId, out var recipe) && recipe != null)
                {
                    mfg.StartCycle(this, recipe, reg.DepositYieldMultiplier, reg.ProductionSpeed);
                }
            }
            else if (mfg.State == Enums.ManufacturingState.Manufacturing)
            {
                // Pause manufacturing progress if in a power deficit and it requires power
                if (_isPowerDeficit && reg.TheoreticalPowerConsumption > 0 && reg.TheoreticalPowerGeneration == 0)
                {
                    // Stalled
                    reg.IsPaused = true;
                }
                else
                {
                    reg.IsPaused = false;
                    mfg.TickWork(delta, this);
                }
            }
        }

        SignalBus.Instance?.EmitContinentEconomyTicked(_continent.StartingIndex);
    }

    private void ProcessQueue()
    {
        if (_requestQueue.Count == 0)
        {
            _queueDirty = false;
            return;
        }

        // Sort by Priority (lowest integer first), then Timestamp
        _requestQueue.Sort((a, b) => 
        {
            int p = a.Priority.CompareTo(b.Priority);
            return p != 0 ? p : a.Timestamp.CompareTo(b.Timestamp);
        });

        for (int i = _requestQueue.Count - 1; i >= 0; i--)
        {
            var request = _requestQueue[i];
            bool fullySatisfied = true;
            var keys = request.MissingResources.Keys.ToList();

            foreach (var res in keys)
            {
                float needed = request.MissingResources[res];
                if (needed <= 0) continue;

                float available = GetStockpile(res);
                float toWithdraw = Math.Min(needed, available);

                if (toWithdraw > 0)
                {
                    WithdrawResource(res, toWithdraw);
                    request.MissingResources[res] -= toWithdraw;
                    request.Building.Manufacturing.DeliverResource(res, toWithdraw);
                }

                if (request.MissingResources[res] > 0.001f)
                {
                    fullySatisfied = false;
                }
            }

            if (fullySatisfied)
            {
                request.Building.Manufacturing.SetState(Enums.ManufacturingState.Manufacturing);
                _requestQueue.RemoveAt(i);
            }
        }

        _queueDirty = false;
    }

    private void TickPower(float delta)
    {
        _powerGeneration = 0f;
        _powerConsumption = 0f;

        // Only calculate power for buildings actively manufacturing or producing power
        foreach (var reg in _activeBuildings)
        {
            var mfg = reg.BuildingNode.Manufacturing;
            if (mfg.State == Enums.ManufacturingState.Manufacturing || mfg.State == Enums.ManufacturingState.WaitingForInputs)
            {
                _powerGeneration += reg.TheoreticalPowerGeneration;
                _powerConsumption += reg.TheoreticalPowerConsumption;
            }
            // Continuous generators always produce if they have no inputs required
            else if (mfg.State == Enums.ManufacturingState.Idle && reg.TheoreticalPowerGeneration > 0 && reg.TheoreticalInputRates.Count == 0)
            {
                 _powerGeneration += reg.TheoreticalPowerGeneration;
            }
        }

        _powerStored += (_powerGeneration - _powerConsumption) * delta;
        _powerStored = Math.Clamp(_powerStored, 0f, _powerStorageCapacity);

        if (!_isPowerDeficit && _powerStored <= 0f && _powerGeneration < _powerConsumption)
        {
            _isPowerDeficit = true;
            GameLogger.Warning($"[ContinentEconomy] Continent {_continent.StartingIndex}: Power deficit!");
            SignalBus.Instance?.EmitContinentPowerStateChanged(_continent.StartingIndex, true);
        }
        else if (_isPowerDeficit && (_powerStored > 0f || _powerGeneration >= _powerConsumption))
        {
            _isPowerDeficit = false;
            GameLogger.Info($"[ContinentEconomy] Continent {_continent.StartingIndex}: Power restored");
            SignalBus.Instance?.EmitContinentPowerStateChanged(_continent.StartingIndex, false);
        }
    }

    public float DepositResource(string resourceId, float amount)
    {
        float capacity = GetCapacityForResource(resourceId);
        float currentCategory = GetCategoryUsed(GetCategoryForResource(resourceId));
        float available = capacity - currentCategory;
        float toDeposit = Math.Min(amount, Math.Max(0f, available));

        if (toDeposit > 0f)
        {
            _stockpile[resourceId] = GetStockpile(resourceId) + toDeposit;
            _queueDirty = true; // New resources available, re-evaluate queue next tick
        }

        return toDeposit;
    }

    public float WithdrawResource(string resourceId, float amount)
    {
        float current = GetStockpile(resourceId);
        float toWithdraw = Math.Min(amount, current);

        if (toWithdraw > 0f)
        {
            _stockpile[resourceId] = current - toWithdraw;
        }

        return toWithdraw;
    }

    public float GetStockpile(string resourceId) => _stockpile.TryGetValue(resourceId, out float val) ? val : 0f;
    public float GetNetRate(string resourceId) => _netRates.TryGetValue(resourceId, out float val) ? val : 0f;
    public float GetProductionRate(string resourceId) => _productionRates.TryGetValue(resourceId, out float val) ? val : 0f;
    public float GetConsumptionRate(string resourceId) => _consumptionRates.TryGetValue(resourceId, out float val) ? val : 0f;

    public float GetCategoryCapacity(string category) => _categoryCapacity.TryGetValue(category, out float val) ? val : DEFAULT_CATEGORY_CAPACITY;

    public float GetCategoryUsed(string category)
    {
        float used = 0f;
        foreach (var kvp in _stockpile)
        {
            if (GetCategoryForResource(kvp.Key) == category) used += kvp.Value;
        }
        return used;
    }

    public void AddStorageCapacity(string category, float amount, BuildingConstruction? building = null)
    {
        if (building != null)
        {
            if (!_storageBuildingsByCategory.ContainsKey(category)) _storageBuildingsByCategory[category] = new();
            _storageBuildingsByCategory[category].Add((building, amount));
        }

        if (_categoryCapacity.ContainsKey(category)) _categoryCapacity[category] += amount;
        else _categoryCapacity[category] = amount;
    }

    public void RemoveStorageCapacity(string category, float amount, BuildingConstruction? building = null)
    {
        if (building != null && _storageBuildingsByCategory.ContainsKey(category))
        {
            var list = _storageBuildingsByCategory[category];
            int idx = list.FindIndex(x => x.Building == building);
            if (idx >= 0) list.RemoveAt(idx);
        }

        if (_categoryCapacity.ContainsKey(category))
            _categoryCapacity[category] = Math.Max(0f, _categoryCapacity[category] - amount);
    }

    public void AddPowerStorageCapacity(float amount) => _powerStorageCapacity += amount;

    public IReadOnlyDictionary<string, float> GetAllStockpiles() => _stockpile;
    public IReadOnlyDictionary<string, float> GetAllNetRates() => _netRates;

    public float GetStorageFillPercentage(BuildingConstruction building, string category)
    {
        if (!_storageBuildingsByCategory.TryGetValue(category, out var buildings)) return 0f;

        float globalUsed = GetCategoryUsed(category);
        float capacityBefore = 0f;

        foreach (var tuple in buildings)
        {
            if (tuple.Building == building)
            {
                float myCapacity = tuple.Capacity;
                if (globalUsed <= capacityBefore) return 0f;
                if (globalUsed >= capacityBefore + myCapacity) return 100f;
                return ((globalUsed - capacityBefore) / myCapacity) * 100f;
            }
            capacityBefore += tuple.Capacity;
        }

        return 0f;
    }

    private void RecomputeTheoreticalRates()
    {
        _productionRates.Clear();
        _consumptionRates.Clear();
        _netRates.Clear();

        foreach (var reg in _activeBuildings)
        {
            foreach (var input in reg.TheoreticalInputRates)
            {
                if (_consumptionRates.ContainsKey(input.Key)) _consumptionRates[input.Key] += input.Value;
                else _consumptionRates[input.Key] = input.Value;
            }

            foreach (var output in reg.TheoreticalOutputRates)
            {
                if (_productionRates.ContainsKey(output.Key)) _productionRates[output.Key] += output.Value;
                else _productionRates[output.Key] = output.Value;
            }
        }

        var allResources = new HashSet<string>(_productionRates.Keys.Concat(_consumptionRates.Keys));
        foreach (var res in allResources)
        {
            float prod = _productionRates.TryGetValue(res, out float p) ? p : 0f;
            float cons = _consumptionRates.TryGetValue(res, out float c) ? c : 0f;
            _netRates[res] = prod - cons;
        }
    }

    private float GetDepositYieldMultiplier(BuildingConstruction building, RecipeDefinition recipe)
    {
        if (recipe.Category != "extraction" || building.PrimaryCell == null) return 1.0f;
        float maxYield = 0f;
        bool foundDeposit = false;
        foreach (var output in recipe.OutputResources)
        {
            if (output.Key == POWER_RESOURCE_ID) continue;
            if (building.PrimaryCell.Resources.TryGetValue(output.Key, out float abundance))
            {
                maxYield = Math.Max(maxYield, abundance);
                foundDeposit = true;
            }
        }
        return foundDeposit ? Math.Max(0.1f, maxYield) : 0.1f;
    }

    private static string GetCategoryForResource(string resourceId)
    {
        var resourceDb = ResourceDatabase.Instance;
        if (resourceDb != null && resourceDb.IsLoaded && resourceDb.TryGetResource(resourceId, out var def) && def?.ResourceType != null)
            return def.ResourceType;

        if (resourceId.EndsWith("_ore")) return "ore";
        if (resourceId == POWER_RESOURCE_ID) return "power";
        return "raw_material";
    }

    private float GetCapacityForResource(string resourceId) => GetCategoryCapacity(GetCategoryForResource(resourceId));

    public class BuildingRegistration
    {
        public int BuildingInstanceId { get; set; }
        public BuildingConstruction BuildingNode { get; set; } = null!;
        public string RecipeId { get; set; } = "";
        public float DepositYieldMultiplier { get; set; } = 1.0f;
        public float ProductionSpeed { get; set; } = 1.0f;
        public Dictionary<string, float> TheoreticalInputRates { get; set; } = new();
        public Dictionary<string, float> TheoreticalOutputRates { get; set; } = new();
        public float TheoreticalPowerConsumption { get; set; }
        public float TheoreticalPowerGeneration { get; set; }
        public bool IsPaused { get; set; }
    }
}
