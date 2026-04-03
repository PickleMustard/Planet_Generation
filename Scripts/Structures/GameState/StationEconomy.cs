using System;
using System.Collections.Generic;
using System.Linq;
using Constructables;
using Structures.Resources;
using UtilityLibrary;

namespace Structures.GameState;

/// <summary>
/// Manages the runtime economy for an orbital station: stockpiles, production/consumption rates,
/// power generation/storage, building registration, and shortage/deficit handling.
/// Parallel to ContinentEconomy but for orbital stations.
/// </summary>
public class StationEconomy : IResourceEndpoint
{
    private const float PAUSE_COOLDOWN_SECONDS = 5.0f;
    private const float DEFAULT_CATEGORY_CAPACITY = 500f;
    private const float DEFAULT_POWER_STORAGE_CAPACITY = 250f;
    private const string POWER_RESOURCE_ID = "power";
    private const string PAUSE_REASON_POWER = "power";
    private const string PAUSE_REASON_SHORTAGE_PREFIX = "shortage:";

    private readonly string _stationId;
    private readonly List<BuildingRegistration> _activeBuildings = new();
    private int _nextBuildingInstanceId;

    // Stockpile
    private readonly Dictionary<string, float> _stockpile = new();
    private readonly Dictionary<string, float> _categoryCapacity = new();

    // Rates (recomputed on building changes)
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

    // Shortage tracking
    private readonly HashSet<string> _activeShortages = new();

    public string StationId => _stationId;
    public bool IsPowerDeficit => _isPowerDeficit;
    public float PowerStored => _powerStored;
    public float PowerStorageCapacity => _powerStorageCapacity;
    public float PowerGeneration => _powerGeneration;
    public float PowerConsumption => _powerConsumption;
    public int ActiveBuildingCount => _activeBuildings.Count;

    public StationEconomy(string stationId)
    {
        _stationId = stationId;
        InitializeDefaultCapacities();
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

    /// <summary>
    /// Registers a completed building with the economy, computing its per-second rates.
    /// </summary>
    public void RegisterBuilding(BuildingConstruction building, string recipeId)
    {
        if (building == null || string.IsNullOrEmpty(recipeId))
        {
            GameLogger.Warning("[StationEconomy] Cannot register building: null building or empty recipe");
            return;
        }

        var recipeDb = RecipeDatabase.Instance;
        if (recipeDb == null || !recipeDb.IsLoaded)
        {
            GameLogger.Error("[StationEconomy] RecipeDatabase not loaded, cannot register building");
            return;
        }

        if (!recipeDb.TryGetRecipe(recipeId, out var recipe) || recipe == null)
        {
            GameLogger.Warning($"[StationEconomy] Recipe '{recipeId}' not found, skipping registration for {building.Name}");
            return;
        }

        float productionSpeed = building.Definition?.Production?.ProductionSpeed ?? 1.0f;
        float cyclesPerSecond = productionSpeed / recipe.WorkRequired;

        var registration = new BuildingRegistration
        {
            BuildingInstanceId = _nextBuildingInstanceId++,
            BuildingNode = building,
            RecipeId = recipeId,
            InputRates = new Dictionary<string, float>(),
            OutputRates = new Dictionary<string, float>(),
            PowerConsumption = 0f,
            PowerGeneration = 0f,
            IsPaused = false,
            PauseReasons = new HashSet<string>(),
            PausedAtTime = 0.0,
        };

        foreach (var input in recipe.InputResources)
        {
            float rate = cyclesPerSecond * input.Value;
            if (input.Key == POWER_RESOURCE_ID)
                registration.PowerConsumption = rate;
            else
                registration.InputRates[input.Key] = rate;
        }

        foreach (var output in recipe.OutputResources)
        {
            float rate = cyclesPerSecond * output.Value;
            if (output.Key == POWER_RESOURCE_ID)
                registration.PowerGeneration = rate;
            else
                registration.OutputRates[output.Key] = rate;
        }

        _activeBuildings.Add(registration);
        _ratesDirty = true;

        GameLogger.Info(
            $"[StationEconomy] Registered building '{building.Name}' with recipe '{recipeId}' " +
            $"(cycles/sec: {cyclesPerSecond:F3})"
        );
    }

    /// <summary>
    /// Unregisters a building from the economy.
    /// </summary>
    public void UnregisterBuilding(BuildingConstruction building)
    {
        int removed = _activeBuildings.RemoveAll(r => r.BuildingNode == building);
        if (removed > 0)
        {
            _ratesDirty = true;
            GameLogger.Info($"[StationEconomy] Unregistered building '{building.Name}'");
        }
    }

    /// <summary>
    /// Changes the active recipe for a registered building.
    /// </summary>
    public void ChangeRecipe(BuildingConstruction building, string newRecipeId)
    {
        UnregisterBuilding(building);
        RegisterBuilding(building, newRecipeId);
    }

    /// <summary>
    /// Main tick called by EconomyManager each physics frame.
    /// </summary>
    public void Tick(float delta, double totalTime)
    {
        if (_ratesDirty)
        {
            RecomputeRates();
            _ratesDirty = false;
        }

        TickPower(delta);
        TickStockpiles(delta);
        TickShortages(delta, totalTime);
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

    public float GetStockpile(string resourceId)
    {
        return _stockpile.TryGetValue(resourceId, out float val) ? val : 0f;
    }

    public float GetNetRate(string resourceId)
    {
        return _netRates.TryGetValue(resourceId, out float val) ? val : 0f;
    }

    public float GetProductionRate(string resourceId)
    {
        return _productionRates.TryGetValue(resourceId, out float val) ? val : 0f;
    }

    public float GetConsumptionRate(string resourceId)
    {
        return _consumptionRates.TryGetValue(resourceId, out float val) ? val : 0f;
    }

    public float GetCategoryCapacity(string category)
    {
        return _categoryCapacity.TryGetValue(category, out float val) ? val : DEFAULT_CATEGORY_CAPACITY;
    }

    public float GetCategoryUsed(string category)
    {
        float used = 0f;
        foreach (var kvp in _stockpile)
        {
            if (GetCategoryForResource(kvp.Key) == category)
            {
                used += kvp.Value;
            }
        }
        return used;
    }

    public void AddStorageCapacity(string category, float amount)
    {
        if (_categoryCapacity.ContainsKey(category))
            _categoryCapacity[category] += amount;
        else
            _categoryCapacity[category] = amount;
    }

    public IReadOnlyDictionary<string, float> GetAllStockpiles()
    {
        return _stockpile;
    }

    public IReadOnlyDictionary<string, float> GetAllNetRates()
    {
        return _netRates;
    }

    private void RecomputeRates()
    {
        _productionRates.Clear();
        _consumptionRates.Clear();
        _netRates.Clear();
        _powerGeneration = 0f;
        _powerConsumption = 0f;

        foreach (var reg in _activeBuildings)
        {
            if (reg.IsPaused)
                continue;

            _powerGeneration += reg.PowerGeneration;
            _powerConsumption += reg.PowerConsumption;

            foreach (var input in reg.InputRates)
            {
                if (_consumptionRates.ContainsKey(input.Key))
                    _consumptionRates[input.Key] += input.Value;
                else
                    _consumptionRates[input.Key] = input.Value;
            }

            foreach (var output in reg.OutputRates)
            {
                if (_productionRates.ContainsKey(output.Key))
                    _productionRates[output.Key] += output.Value;
                else
                    _productionRates[output.Key] = output.Value;
            }
        }

        var allResources = new HashSet<string>();
        foreach (var key in _productionRates.Keys)
            allResources.Add(key);
        foreach (var key in _consumptionRates.Keys)
            allResources.Add(key);

        foreach (var res in allResources)
        {
            float prod = _productionRates.TryGetValue(res, out float p) ? p : 0f;
            float cons = _consumptionRates.TryGetValue(res, out float c) ? c : 0f;
            _netRates[res] = prod - cons;
        }
    }

    private void TickPower(float delta)
    {
        _powerStored += (_powerGeneration - _powerConsumption) * delta;
        _powerStored = Math.Clamp(_powerStored, 0f, _powerStorageCapacity);

        if (!_isPowerDeficit && _powerStored <= 0f && _powerGeneration < _powerConsumption)
        {
            _isPowerDeficit = true;
            PauseAllNonPowerBuildings();
            GameLogger.Warning(
                $"[StationEconomy] Station {_stationId}: Power deficit! " +
                $"Gen={_powerGeneration:F1}/s, Cons={_powerConsumption:F1}/s"
            );
        }
        else if (_isPowerDeficit && (_powerStored > 0f || _powerGeneration >= _powerConsumption))
        {
            _isPowerDeficit = false;
            UnpauseBuildingsForReason(PAUSE_REASON_POWER);
            GameLogger.Info($"[StationEconomy] Station {_stationId}: Power restored");
        }
    }

    private void TickStockpiles(float delta)
    {
        foreach (var kvp in _netRates)
        {
            float current = GetStockpile(kvp.Key);
            float newVal = current + kvp.Value * delta;

            newVal = Math.Max(0f, newVal);

            string category = GetCategoryForResource(kvp.Key);
            float capacity = GetCategoryCapacity(category);
            float categoryUsed = GetCategoryUsed(category) - current + newVal;
            if (categoryUsed > capacity)
            {
                newVal -= (categoryUsed - capacity);
                newVal = Math.Max(0f, newVal);
            }

            _stockpile[kvp.Key] = newVal;
        }
    }

    private void TickShortages(float delta, double totalTime)
    {
        foreach (var kvp in _netRates)
        {
            string resourceId = kvp.Key;
            float stockpile = GetStockpile(resourceId);
            float netRate = kvp.Value;
            string shortageReason = PAUSE_REASON_SHORTAGE_PREFIX + resourceId;

            if (stockpile <= 0f && netRate < 0f && !_activeShortages.Contains(resourceId))
            {
                _activeShortages.Add(resourceId);
                PauseConsumersOf(resourceId, totalTime);
                GameLogger.Warning($"[StationEconomy] Station {_stationId}: Shortage of '{resourceId}'");
            }
            else if (_activeShortages.Contains(resourceId) && (stockpile > 0f || netRate >= 0f))
            {
                _activeShortages.Remove(resourceId);
                UnpauseBuildingsForReason(shortageReason, totalTime);
                GameLogger.Info($"[StationEconomy] Station {_stationId}: Shortage of '{resourceId}' resolved");
            }
        }
    }

    private void PauseAllNonPowerBuildings()
    {
        bool changed = false;
        foreach (var reg in _activeBuildings)
        {
            if (reg.PowerGeneration > 0f)
                continue;

            if (reg.PauseReasons.Add(PAUSE_REASON_POWER))
            {
                reg.IsPaused = true;
                changed = true;
            }
        }

        if (changed)
            _ratesDirty = true;
    }

    private void PauseConsumersOf(string resourceId, double totalTime)
    {
        string reason = PAUSE_REASON_SHORTAGE_PREFIX + resourceId;
        bool changed = false;

        for (int i = _activeBuildings.Count - 1; i >= 0; i--)
        {
            var reg = _activeBuildings[i];
            if (reg.IsPaused)
                continue;

            if (reg.InputRates.ContainsKey(resourceId))
            {
                reg.PauseReasons.Add(reason);
                reg.IsPaused = true;
                reg.PausedAtTime = totalTime;
                changed = true;

                _ratesDirty = true;
                RecomputeRates();
                _ratesDirty = false;

                float newNet = _netRates.TryGetValue(resourceId, out float n) ? n : 0f;
                if (newNet >= 0f)
                    break;
            }
        }

        if (changed)
            _ratesDirty = true;
    }

    private void UnpauseBuildingsForReason(string reason, double totalTime = 0.0)
    {
        bool changed = false;

        for (int i = 0; i < _activeBuildings.Count; i++)
        {
            var reg = _activeBuildings[i];
            if (!reg.PauseReasons.Contains(reason))
                continue;

            if (totalTime > 0.0 && totalTime - reg.PausedAtTime < PAUSE_COOLDOWN_SECONDS)
                continue;

            reg.PauseReasons.Remove(reason);
            if (reg.PauseReasons.Count == 0)
            {
                reg.IsPaused = false;
                changed = true;
            }
        }

        if (changed)
            _ratesDirty = true;
    }

    private static string GetCategoryForResource(string resourceId)
    {
        var resourceDb = ResourceDatabase.Instance;
        if (resourceDb != null && resourceDb.IsLoaded
            && resourceDb.TryGetResource(resourceId, out var def) && def?.ResourceType != null)
        {
            return def.ResourceType;
        }

        if (resourceId.EndsWith("_ore"))
            return "ore";
        if (resourceId == POWER_RESOURCE_ID)
            return "power";
        return "raw_material";
    }

    private float GetCapacityForResource(string resourceId)
    {
        string category = GetCategoryForResource(resourceId);
        return GetCategoryCapacity(category);
    }

    /// <summary>
    /// Tracks a building registered with the station economy.
    /// </summary>
    public class BuildingRegistration
    {
        public int BuildingInstanceId { get; set; }
        public BuildingConstruction BuildingNode { get; set; } = null!;
        public string RecipeId { get; set; } = "";
        public Dictionary<string, float> InputRates { get; set; } = new();
        public Dictionary<string, float> OutputRates { get; set; } = new();
        public float PowerConsumption { get; set; }
        public float PowerGeneration { get; set; }
        public bool IsPaused { get; set; }
        public HashSet<string> PauseReasons { get; set; } = new();
        public double PausedAtTime { get; set; }
    }
}
