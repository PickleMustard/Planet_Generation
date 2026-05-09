using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.GameState;
using Structures.Resources;
using Structures.Transfers;
using UI.Components;

namespace UI.ContinentInfo;

/// <summary>
/// Bottom details panel that updates its content based on the ContinentTabbedPanel selection.
/// Renders continent-aggregate info, cell info, building aggregates, single-building metrics,
/// power summaries, generator/battery detail, and transfer/schedule manifests.
/// </summary>
public partial class ContinentDetailsPanel : PanelContainer
{
    private static readonly PackedScene StoredResourceItemScene = GD.Load<PackedScene>(
        "res://UI/ContinentInfo/StoredResourceItem.tscn"
    );

    private Label? _titleLabel;
    private VBoxContainer? _contentContainer;

    private int _continentIndex = -1;
    private IOrbitalBody? _body;
    private Continent? _continent;
    private ContinentTabbedPanel? _tabbedPanel;

    private string? _lastItemType;
    private int _lastItemIndex = -1;

    public override void _Ready()
    {
        _titleLabel = GetNode<Label>("VBoxContainer/DetailTitleLabel");
        _contentContainer = GetNode<VBoxContainer>("VBoxContainer/DetailScroll/DetailContent");
    }

    public void Initialize(
        int continentIndex,
        IOrbitalBody body,
        Continent continent,
        ContinentTabbedPanel tabbedPanel
    )
    {
        _continentIndex = continentIndex;
        _body = body;
        _continent = continent;
        _tabbedPanel = tabbedPanel;
        tabbedPanel.ItemSelected += OnItemSelected;
    }

    public void Disconnect(ContinentTabbedPanel tabbedPanel)
    {
        tabbedPanel.ItemSelected -= OnItemSelected;
    }

    public void Clear()
    {
        _body = null;
        _continent = null;
        _tabbedPanel = null;
        _continentIndex = -1;
        _lastItemType = null;
        _lastItemIndex = -1;
        ClearContent();
        if (_titleLabel != null)
            _titleLabel.Text = "Details";
    }

    public void RefreshCurrentDetails()
    {
        if (_continent == null || _lastItemType == null)
            return;

        OnItemSelected(_lastItemType, _lastItemIndex);
    }

    private void OnItemSelected(string itemType, int itemIndex)
    {
        _lastItemType = itemType;
        _lastItemIndex = itemIndex;
        ClearContent();

        switch (itemType)
        {
            case "continent_aggregate":
                ShowContinentAggregate();
                break;
            case "cell":
                ShowCellDetails(itemIndex);
                break;
            case "building_type":
                ShowBuildingTypeDetails(itemIndex);
                break;
            case "building":
                ShowBuildingDetails(itemIndex);
                break;
            case "power_summary":
                ShowPowerSummary();
                break;
            case "power_generator":
                ShowPowerGeneratorDetails(itemIndex);
                break;
            case "battery":
                ShowBatteryDetails(itemIndex);
                break;
            case "transfer":
                ShowActiveTransferDetails(itemIndex);
                break;
            case "schedule":
                ShowScheduleDetails(itemIndex);
                break;
        }
    }

    // ───────── Continent Aggregate ─────────

    private void ShowContinentAggregate()
    {
        if (_continent == null || _titleLabel == null || _contentContainer == null)
            return;

        _titleLabel.Text = $"Continent {_continentIndex}";

        AddDetailHeader("Geography");
        AddDetailRow("Crust Type", _continent.elevation.ToString());
        AddDetailRow("Cells", _continent.cells.Count.ToString());
        AddDetailRow("Avg Height", $"{_continent.averageHeight:F2}");
        AddDetailRow("Avg Moisture", $"{_continent.averageMoisture:F2}");

        // Biome distribution
        var biomeCounts = _continent
            .cells.GroupBy(c => c.Biome)
            .Select(g => (Biome: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();
        if (biomeCounts.Count > 0)
        {
            AddSeparator();
            AddDetailHeader("Biomes");
            foreach (var entry in biomeCounts)
                AddDetailRow(entry.Biome.ToString(), entry.Count.ToString());
        }

        // Available resources (from continent.ResourceAbundance)
        if (_continent.ResourceAbundance.Count > 0)
        {
            AddSeparator();
            AddDetailHeader("Available Resources");
            foreach (var kvp in _continent.ResourceAbundance.OrderByDescending(k => k.Value))
                AddDetailRow(FormatResourceLabel(kvp.Key), $"{kvp.Value:F2}");
        }
    }

    private void RenderStorageItems(List<KeyValuePair<string, float>> stockpiles)
    {
        if (_contentContainer == null)
            return;

        var groups = new Dictionary<string, List<KeyValuePair<string, float>>>();
        foreach (var kv in stockpiles)
        {
            string cat = GetResourceCategory(kv.Key);
            if (!groups.TryGetValue(cat, out var list))
            {
                list = new List<KeyValuePair<string, float>>();
                groups[cat] = list;
            }
            list.Add(kv);
        }
    }

    // ───────── Cell ─────────

    private void ShowCellDetails(int cellIndex)
    {
        if (_continent == null || _titleLabel == null)
            return;

        VoronoiCell? cell = _continent.cells.FirstOrDefault(c => c.Index == cellIndex);
        if (cell == null)
            return;

        _titleLabel.Text = $"Cell {cellIndex}";

        AddDetailRow("Biome", cell.Biome.ToString());
        AddDetailRow("Height", $"{cell.Height:F2}");
        AddDetailRow("Slope", $"{cell.GetSlope():F2}");
        AddDetailRow("Border Tile", cell.IsBorderTile ? "Yes" : "No");

        if (cell.Resources.Count > 0)
        {
            AddSeparator();
            AddDetailHeader("Deposits");
            foreach (var kv in cell.Resources.OrderByDescending(k => k.Value))
                AddDetailRow(FormatResourceLabel(kv.Key), $"{kv.Value:F2}");
        }

        AddSeparator();
        AddDetailHeader("Occupant");
        string occupant = cell.Building?.Definition?.DisplayName ?? "None";
        AddDetailRow("Building", occupant);
    }

    // ───────── Building Type (aggregate) ─────────

    private void ShowBuildingTypeDetails(int groupIndex)
    {
        if (_tabbedPanel == null || _titleLabel == null)
            return;

        // Summed inputs (skip paused buildings)
        var summedInputs = new Dictionary<string, float>();
        var summedOutputs = new Dictionary<string, float>();
        float totalPower = 0f;

        AddDetailRow("Power Draw", $"{totalPower:F1}/s");

        if (summedInputs.Count > 0)
        {
            AddSeparator();
            AddDetailHeader("Inputs/s");
            foreach (var kv in summedInputs.OrderByDescending(k => k.Value))
                AddDetailRow(FormatResourceLabel(kv.Key), $"{kv.Value:F2}");
        }

        if (summedOutputs.Count > 0)
        {
            AddSeparator();
            AddDetailHeader("Outputs/s");
            foreach (var kv in summedOutputs.OrderByDescending(k => k.Value))
                AddDetailRow(FormatResourceLabel(kv.Key), $"{kv.Value:F2}");
        }
    }

    // ───────── Single Building ─────────

    private void ShowBuildingDetails(int buildingInstanceId) { }

    // ───────── Power Summary ─────────

    private void ShowPowerSummary() { }

    // ───────── Power Generator ─────────

    private void ShowPowerGeneratorDetails(int buildingInstanceId) { }

    // ───────── Battery ─────────

    private void ShowBatteryDetails(int buildingInstanceId)
    {}

    // ───────── Transfers ─────────

    private void ShowActiveTransferDetails(int index)
    {}

    private void ShowScheduleDetails(int index)
    {}

    private void RenderManifest(string headerText, CargoManifest manifest)
    {}

    // ───────── Helpers ─────────

    private static string FormatDestination(TransferDestination dest)
    {
        if (dest.IsOrbitalStation)
            return $"Station {dest.StationSatelliteId?[..System.Math.Min(8, dest.StationSatelliteId.Length)]}";
        if (!string.IsNullOrEmpty(dest.BuildingId))
            return $"Hub {dest.BuildingId[..System.Math.Min(8, dest.BuildingId.Length)]}";
        return "Unknown";
    }

    private static string GetResourceCategory(string resourceId)
    {
        if (
            ResourceDatabase.Instance != null
            && ResourceDatabase.Instance.TryGetResource(resourceId, out var def)
            && def?.ResourceType != null
        )
        {
            return def.ResourceType;
        }
        if (resourceId.EndsWith("_ore"))
            return "ore";
        return "raw_material";
    }

    private static string FormatCategoryName(string category)
    {
        if (string.IsNullOrEmpty(category))
            return "Other";
        return char.ToUpperInvariant(category[0]) + category.Substring(1);
    }

    private static string FormatResourceLabel(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId))
            return "Unknown";
        var parts = resourceId.Split('_');
        return string.Join(
            " ",
            parts.Select(p =>
                string.IsNullOrEmpty(p) ? p : char.ToUpperInvariant(p[0]) + p.Substring(1)
            )
        );
    }

    private void ClearContent() => DetailRowBuilder.Clear(_contentContainer);

    private void AddDetailRow(string key, string value) =>
        DetailRowBuilder.AddRow(_contentContainer, key, value);

    private void AddPercentRow(
        string key,
        float current,
        float max,
        string? unit = null,
        DonutChart.ColorMode mode = DonutChart.ColorMode.GreenToRed
    ) => DetailRowBuilder.AddPercentRow(_contentContainer, key, current, max, unit, "F1", mode);

    private void AddPercentRow(
        string key,
        int current,
        int max,
        DonutChart.ColorMode mode = DonutChart.ColorMode.RedToGreen
    ) => DetailRowBuilder.AddPercentRow(_contentContainer, key, current, max, mode);

    private void AddProgressRow(
        string key,
        float ratio,
        DonutChart.ColorMode mode = DonutChart.ColorMode.RedToGreen
    ) => DetailRowBuilder.AddProgressRow(_contentContainer, key, ratio, mode);

    private void AddDetailHeader(string text) =>
        DetailRowBuilder.AddHeader(_contentContainer, text);

    private void AddAlertRow(string message) =>
        DetailRowBuilder.AddAlert(_contentContainer, message);

    private void AddSeparator() => DetailRowBuilder.AddSeparator(_contentContainer);
}
