using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.GameState;
using Structures.Resources;
using UtilityLibrary;

namespace UI.TransferPlanning;

/// <summary>
/// Manifest builder: add resources from stockpile, set amounts, track capacity.
/// Shows production rates as context.
/// </summary>
public partial class CargoManifestPanel : Control
{
    public event Action? ManifestChanged;

    private ProgressBar? _capacityBar;
    private Label? _capacityLabel;
    private VBoxContainer? _itemList;
    private Button? _addResourceButton;
    private PopupMenu? _addResourcePopup;
    private Label? _emptyLabel;

    private ContinentEconomy? _economy;
    private float _totalCapacity;
    private readonly List<CargoManifestItem> _items = new();
    private readonly HashSet<string> _addedResourceIds = new();
    private bool _isUpdating;
    private double _lastUpdateTime;
    private const double UPDATE_DEBOUNCE_SECONDS = 0.5;

    public override void _Ready()
    {
        _capacityBar = GetNodeOrNull<ProgressBar>("MarginContainer/VBoxContainer/CapacityContainer/CapacityBar");
        _capacityLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/CapacityContainer/CapacityLabel");
        _itemList = GetNodeOrNull<VBoxContainer>("MarginContainer/VBoxContainer/ScrollContainer/ItemList");
        _addResourceButton = GetNodeOrNull<Button>("MarginContainer/VBoxContainer/AddResourceButton");
        _emptyLabel = GetNodeOrNull<Label>("MarginContainer/VBoxContainer/EmptyLabel");

        if (_addResourceButton != null)
        {
            _addResourcePopup = new PopupMenu();
            _addResourceButton.AddChild(_addResourcePopup);
            _addResourceButton.Pressed += OnAddResourcePressed;
            _addResourcePopup.IdPressed += OnResourcePopupSelected;
        }

        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.ContinentEconomyTicked += OnEconomyTicked;
        }
    }

    /// <summary>
    /// Initializes the manifest panel with economy data and cargo capacity.
    /// </summary>
    public void Initialize(ContinentEconomy economy, float totalCapacity)
    {
        Clear();
        _economy = economy;
        _totalCapacity = totalCapacity;
        UpdateCapacityDisplay();

        if (_emptyLabel != null)
            _emptyLabel.Visible = true;
    }

    public void UpdateCapacity(float newCapacity)
    {
        _totalCapacity = newCapacity;
        RecalculateMaxAmounts();
        UpdateCapacityDisplay();
    }

    public void Clear()
    {
        foreach (var item in _items)
        {
            item.QueueFree();
        }
        _items.Clear();
        _addedResourceIds.Clear();
        _economy = null;
        UpdateCapacityDisplay();

        if (_emptyLabel != null)
            _emptyLabel.Visible = true;
    }

    public float GetUsedCapacity()
    {
        float used = 0f;
        foreach (var item in _items)
        {
            used += item.Weight;
        }
        return used;
    }

    public int GetResourceCount() => _items.Count;

    /// <summary>
    /// Returns resource amounts as a dictionary (for dispatch).
    /// </summary>
    public Dictionary<string, float> GetManifestResources()
    {
        var result = new Dictionary<string, float>();
        foreach (var item in _items)
        {
            float amount = item.Amount;
            if (amount > 0f)
                result[item.ResourceId] = amount;
        }
        return result;
    }

    private void OnAddResourcePressed()
    {
        if (_economy == null || _addResourcePopup == null)
            return;

        _addResourcePopup.Clear();

        var stockpiles = _economy.GetAllStockpiles();
        var resourceDb = ResourceDatabase.Instance;
        int index = 0;

        foreach (var kvp in stockpiles.OrderBy(s => s.Key))
        {
            if (kvp.Value <= 0.01f || _addedResourceIds.Contains(kvp.Key))
                continue;

            string displayName = kvp.Key;
            if (resourceDb?.IsLoaded == true && resourceDb.TryGetResource(kvp.Key, out var def) && def != null)
            {
                displayName = FormatResourceName(def.IdName ?? kvp.Key);
            }

            _addResourcePopup.AddItem($"{displayName} ({FormatAmount(kvp.Value)})", index);
            _addResourcePopup.SetItemMetadata(index, kvp.Key);
            index++;
        }

        if (index == 0)
        {
            _addResourcePopup.AddItem("No resources available", -1);
            _addResourcePopup.SetItemDisabled(0, true);
        }

        // Show popup below the button
        var buttonRect = _addResourceButton!.GetGlobalRect();
        _addResourcePopup.Position = new Vector2I(
            (int)buttonRect.Position.X,
            (int)(buttonRect.Position.Y + buttonRect.Size.Y));
        _addResourcePopup.Popup();
    }

    private void OnResourcePopupSelected(long id)
    {
        if (_addResourcePopup == null || _economy == null)
            return;

        // Find the item index with this id
        int itemIndex = -1;
        for (int i = 0; i < _addResourcePopup.ItemCount; i++)
        {
            if (_addResourcePopup.GetItemId(i) == (int)id)
            {
                itemIndex = i;
                break;
            }
        }

        if (itemIndex < 0)
            return;

        string? resourceId = _addResourcePopup.GetItemMetadata(itemIndex).AsString();
        if (string.IsNullOrEmpty(resourceId) || _addedResourceIds.Contains(resourceId))
            return;

        AddManifestItem(resourceId);
    }

    private void AddManifestItem(string resourceId)
    {
        if (_economy == null || _itemList == null)
            return;

        var item = (CargoManifestItem)GD.Load<PackedScene>(
            "res://UI/TransferPlanning/CargoManifestItem.tscn").Instantiate();

        // Add to tree first (so _Ready() fires)
        _itemList.AddChild(item);

        float stockpile = _economy.GetStockpile(resourceId);
        float netRate = _economy.GetNetRate(resourceId);

        ResourceDefinition? definition = null;
        var resourceDb = ResourceDatabase.Instance;
        if (resourceDb?.IsLoaded == true)
            resourceDb.TryGetResource(resourceId, out definition);

        float transportWeight = definition?.TransportWeight ?? 1.0f;
        float remainingCapacity = _totalCapacity - GetUsedCapacity();
        float maxUnits = transportWeight > 0f ? remainingCapacity / transportWeight : 0f;
        float maxAmount = Mathf.Min(stockpile, maxUnits);

        item.SetResource(resourceId, stockpile, netRate, maxAmount, definition);
        item.AmountChanged += OnItemAmountChanged;
        item.RemoveRequested += OnItemRemoveRequested;

        _items.Add(item);
        _addedResourceIds.Add(resourceId);

        if (_emptyLabel != null)
            _emptyLabel.Visible = false;

        RecalculateMaxAmounts();
        UpdateCapacityDisplay();
        ManifestChanged?.Invoke();
    }

    private void OnItemAmountChanged()
    {
        if (_isUpdating) return;
        RecalculateMaxAmounts();
        UpdateCapacityDisplay();
        ManifestChanged?.Invoke();
    }

    private void OnItemRemoveRequested(CargoManifestItem item)
    {
        _items.Remove(item);
        _addedResourceIds.Remove(item.ResourceId);
        item.QueueFree();

        if (_emptyLabel != null)
            _emptyLabel.Visible = _items.Count == 0;

        RecalculateMaxAmounts();
        UpdateCapacityDisplay();
        ManifestChanged?.Invoke();
    }

    private void RecalculateMaxAmounts()
    {
        if (_isUpdating) return;
        _isUpdating = true;

        float usedByOthers = 0f;
        foreach (var item in _items)
        {
            // For each item, compute max based on capacity minus all other items' weights
            usedByOthers = 0f;
            foreach (var other in _items)
            {
                if (other != item)
                    usedByOthers += other.Weight;
            }

            float remainingCapacity = Mathf.Max(_totalCapacity - usedByOthers, 0f);

            ResourceDefinition? def = null;
            var resourceDb = ResourceDatabase.Instance;
            if (resourceDb?.IsLoaded == true)
                resourceDb.TryGetResource(item.ResourceId, out def);

            float transportWeight = def?.TransportWeight ?? 1.0f;
            float maxByCapacity = transportWeight > 0f ? remainingCapacity / transportWeight : 0f;

            float stockpile = _economy?.GetStockpile(item.ResourceId) ?? 0f;
            float maxAmount = Mathf.Min(stockpile, maxByCapacity);

            item.SetMaxAmount(maxAmount);
        }

        _isUpdating = false;
    }

    private void UpdateCapacityDisplay()
    {
        float used = GetUsedCapacity();

        if (_capacityBar != null)
        {
            _capacityBar.MaxValue = _totalCapacity > 0f ? _totalCapacity : 1f;
            _capacityBar.Value = used;
        }

        if (_capacityLabel != null)
        {
            _capacityLabel.Text = $"{used:F0} / {_totalCapacity:F0}";
        }
    }

    private void OnEconomyTicked(int continentIndex)
    {
        if (_economy == null || !IsVisibleInTree())
            return;

        // Check if this tick is for our continent
        if (_economy.Continent?.StartingIndex != continentIndex)
            return;

        double currentTime = Time.GetUnixTimeFromSystem();
        if (currentTime - _lastUpdateTime < UPDATE_DEBOUNCE_SECONDS)
            return;
        _lastUpdateTime = currentTime;

        // Update stockpile and rate displays on existing items
        foreach (var item in _items)
        {
            float stockpile = _economy.GetStockpile(item.ResourceId);
            float netRate = _economy.GetNetRate(item.ResourceId);
            item.UpdateStockpileDisplay(stockpile);
            item.UpdateRateDisplay(netRate);
        }

        RecalculateMaxAmounts();
        UpdateCapacityDisplay();
    }

    private static string FormatAmount(float amount)
    {
        if (amount >= 1_000_000f) return $"{amount / 1_000_000f:F1}M";
        if (amount >= 1_000f) return $"{amount / 1_000f:F1}K";
        return $"{amount:F1}";
    }

    private static string FormatResourceName(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId)) return "Unknown";
        var words = resourceId.Split('_');
        return string.Join(" ", words.Select(w =>
            string.IsNullOrEmpty(w) ? w : char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant()));
    }

    public override void _ExitTree()
    {
        if (SignalBus.Instance != null)
        {
            SignalBus.Instance.ContinentEconomyTicked -= OnEconomyTicked;
        }
    }
}
