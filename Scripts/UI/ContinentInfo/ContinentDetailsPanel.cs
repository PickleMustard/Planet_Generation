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
    private static readonly PackedScene StoredResourceItemScene =
        GD.Load<PackedScene>("res://UI/ContinentInfo/StoredResourceItem.tscn");

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
        ContinentTabbedPanel tabbedPanel)
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
        var biomeCounts = _continent.cells
            .GroupBy(c => c.Biome)
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

        // Extracted resources (positive net rates)
        var economy = _continent.Economy;
        if (economy != null)
        {
            var extracted = economy.GetAllNetRates()
                .Where(kv => kv.Value > 0f && kv.Key != "power")
                .OrderByDescending(kv => kv.Value)
                .ToList();
            if (extracted.Count > 0)
            {
                AddSeparator();
                AddDetailHeader("Extracted Resources");
                foreach (var kv in extracted)
                    AddDetailRow(FormatResourceLabel(kv.Key), $"{kv.Value:F2}/s");
            }

            // Resource storage (stockpiles grouped by category)
            var stockpiles = economy.GetAllStockpiles()
                .Where(kv => kv.Value > 0f && kv.Key != "power")
                .ToList();
            if (stockpiles.Count > 0)
            {
                AddSeparator();
                AddDetailHeader("Resource Storage");
                RenderStorageItems(stockpiles, economy);
            }
        }
    }

    private void RenderStorageItems(
        List<KeyValuePair<string, float>> stockpiles,
        ContinentEconomy economy)
    {
        if (_contentContainer == null) return;

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

        foreach (var group in groups.OrderBy(g => g.Key))
        {
            float capacity = economy.GetCategoryCapacity(group.Key);
            float used = economy.GetCategoryUsed(group.Key);
            AddPercentRow($"[{FormatCategoryName(group.Key)}]", used, capacity);

            foreach (var kv in group.Value.OrderByDescending(x => x.Value))
            {
                ResourceDefinition? def = null;
                ResourceDatabase.Instance?.TryGetResource(kv.Key, out def);
                float pct = capacity > 0f ? kv.Value / capacity : 0f;

                var item = StoredResourceItemScene.Instantiate<StoredResourceItem>();
                _contentContainer.AddChild(item);
                item.SetResource(kv.Key, kv.Value, pct, def);
            }
        }
    }

    // ───────── Cell ─────────

    private void ShowCellDetails(int cellIndex)
    {
        if (_continent == null || _titleLabel == null) return;

        VoronoiCell? cell = _continent.cells.FirstOrDefault(c => c.Index == cellIndex);
        if (cell == null) return;

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
        if (_tabbedPanel == null || _titleLabel == null) return;

        var groups = _tabbedPanel.ManufacturingGroups;
        if (groupIndex < 0 || groupIndex >= groups.Count) return;

        var group = groups[groupIndex];
        if (group.Count == 0) return;

        var first = group[0];
        _titleLabel.Text = first.BuildingNode.Definition?.DisplayName ?? "Building Type";

        int total = group.Count;
        int paused = group.Count(r => r.IsPaused);
        int active = total - paused;

        AddDetailHeader("Aggregate");
        AddDetailRow("Count", total.ToString());
        AddPercentRow("Active", active, total);
        float efficiency = total > 0 ? (float)active / total : 0f;
        AddDetailRow("Avg Efficiency", $"{efficiency * 100f:F0}%");

        // Summed inputs (skip paused buildings)
        var summedInputs = new Dictionary<string, float>();
        var summedOutputs = new Dictionary<string, float>();
        float totalPower = 0f;
        foreach (var reg in group)
        {
            if (reg.IsPaused) continue;
            foreach (var kv in reg.TheoreticalInputRates)
                summedInputs[kv.Key] = summedInputs.GetValueOrDefault(kv.Key) + kv.Value;
            foreach (var kv in reg.TheoreticalOutputRates)
                summedOutputs[kv.Key] = summedOutputs.GetValueOrDefault(kv.Key) + kv.Value;
            totalPower += reg.TheoreticalPowerConsumption;
        }

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

    private void ShowBuildingDetails(int buildingInstanceId)
    {
        var reg = FindRegistration(buildingInstanceId);
        if (reg == null || _titleLabel == null || _contentContainer == null) return;

        string displayName = reg.BuildingNode.Definition?.DisplayName ?? "Building";
        _titleLabel.Text = displayName;

        // Icon
        var iconTex = reg.BuildingNode.Definition?.Icon?.MediumTexture;
        if (iconTex != null)
        {
            var iconRect = new TextureRect
            {
                Texture = iconTex,
                ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(48, 48),
            };
            _contentContainer.AddChild(iconRect);
        }

        AddDetailRow("Status", reg.IsPaused ? "Paused" : "Active");
        AddDetailRow("Efficiency", reg.IsPaused ? "0%" : "100%");
        AddDetailRow("Power Draw", $"{reg.TheoreticalPowerConsumption:F1}/s");

        // Recipe
        if (!string.IsNullOrEmpty(reg.RecipeId)
            && RecipeDatabase.Instance != null
            && RecipeDatabase.Instance.TryGetRecipe(reg.RecipeId, out var recipe)
            && recipe != null)
        {
            AddSeparator();
            AddDetailHeader("Recipe");
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);

            var recipeIcon = recipe.Icon?.SmallTexture ?? recipe.Icon?.MediumTexture;
            if (recipeIcon != null)
            {
                var rect = new TextureRect
                {
                    Texture = recipeIcon,
                    CustomMinimumSize = new Vector2(24, 24),
                    ExpandMode = TextureRect.ExpandModeEnum.FitWidthProportional,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                };
                row.AddChild(rect);
            }
            var label = new Label { Text = recipe.DisplayName ?? reg.RecipeId };
            label.AddThemeFontSizeOverride("font_size", 13);
            row.AddChild(label);
            _contentContainer.AddChild(row);
        }

        if (reg.TheoreticalInputRates.Count > 0)
        {
            AddSeparator();
            AddDetailHeader("Inputs/s");
            foreach (var kv in reg.TheoreticalInputRates)
                AddDetailRow(FormatResourceLabel(kv.Key), $"{kv.Value:F2}");
        }

        if (reg.TheoreticalOutputRates.Count > 0)
        {
            AddSeparator();
            AddDetailHeader("Outputs/s");
            foreach (var kv in reg.TheoreticalOutputRates)
                AddDetailRow(FormatResourceLabel(kv.Key), $"{kv.Value:F2}");
        }
    }

    // ───────── Power Summary ─────────

    private void ShowPowerSummary()
    {
        if (_continent?.Economy == null || _titleLabel == null) return;
        var eco = _continent.Economy;

        _titleLabel.Text = "Power Summary";

        AddDetailRow("Generation", $"{eco.PowerGeneration:F1}/s");
        AddDetailRow("Consumption", $"{eco.PowerConsumption:F1}/s");
        AddDetailRow("Net", $"{(eco.PowerGeneration - eco.PowerConsumption):F1}/s");
        AddSeparator();
        AddPercentRow("Stored", eco.PowerStored, eco.PowerStorageCapacity, mode: DonutChart.ColorMode.RedToGreen);

        if (eco.IsPowerDeficit)
        {
            AddSeparator();
            AddAlertRow("POWER DEFICIT");
        }
    }

    // ───────── Power Generator ─────────

    private void ShowPowerGeneratorDetails(int buildingInstanceId)
    {
        var reg = FindRegistration(buildingInstanceId);
        if (reg == null || _titleLabel == null) return;

        _titleLabel.Text = reg.BuildingNode.Definition?.DisplayName ?? "Generator";

        AddDetailRow("Status", reg.IsPaused ? "Paused" : "Active");
        AddDetailRow("Efficiency", reg.IsPaused ? "0%" : "100%");
        AddDetailRow("Generation", $"{reg.TheoreticalPowerGeneration:F1}/s");

        if (reg.TheoreticalInputRates.Count > 0)
        {
            AddSeparator();
            AddDetailHeader("Fuel Inputs/s");
            foreach (var kv in reg.TheoreticalInputRates)
                AddDetailRow(FormatResourceLabel(kv.Key), $"{kv.Value:F2}");
        }

        if (reg.TheoreticalOutputRates.Count > 0)
        {
            AddSeparator();
            AddDetailHeader("Byproducts/s");
            foreach (var kv in reg.TheoreticalOutputRates)
                AddDetailRow(FormatResourceLabel(kv.Key), $"{kv.Value:F2}");
        }
    }

    // ───────── Battery ─────────

    private void ShowBatteryDetails(int buildingInstanceId)
    {
        var reg = FindRegistration(buildingInstanceId);
        if (reg == null || _titleLabel == null || _continent?.Economy == null) return;

        _titleLabel.Text = reg.BuildingNode.Definition?.DisplayName ?? "Battery";

        var eco = _continent.Economy;
        float capacity = ContinentTabbedPanel.GetBatteryCapacity(reg);

        AddDetailHeader("This Battery");
        AddDetailRow("Capacity Contribution", $"{capacity:F1}");

        AddSeparator();
        AddDetailHeader("Continent Pool");
        AddPercentRow("Stored", eco.PowerStored, eco.PowerStorageCapacity, mode: DonutChart.ColorMode.RedToGreen);

        AddSeparator();
        var note = new Label { Text = "Note: per-battery stored amount is pooled continent-wide." };
        note.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f));
        note.AddThemeFontSizeOverride("font_size", 11);
        note.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _contentContainer?.AddChild(note);
    }

    // ───────── Transfers ─────────

    private void ShowActiveTransferDetails(int index)
    {
        if (_tabbedPanel == null || _titleLabel == null) return;
        var snapshot = _tabbedPanel.ActiveTransferSnapshot;
        if (index < 0 || index >= snapshot.Count) return;

        var order = snapshot[index].Order;
        _titleLabel.Text = $"Transfer {order.OrderId[..8]}";

        AddDetailRow("State", order.State.ToString());
        AddDetailRow("Origin", $"Continent {order.OriginContinentIndex}");
        AddDetailRow("Destination", FormatDestination(order.Destination));
        AddProgressRow("Progress", order.Progress);
        AddPercentRow("Travel Time", order.ElapsedTimeSeconds, order.TravelTimeSeconds, "s");

        RenderManifest("Manifest", order.Manifest);
        if (order.RequestedManifest.ResourceCount > 0
            && order.RequestedManifest.ResourceCount != order.Manifest.ResourceCount)
        {
            RenderManifest("Requested", order.RequestedManifest);
        }
    }

    private void ShowScheduleDetails(int index)
    {
        if (_tabbedPanel == null || _titleLabel == null) return;
        var snapshot = _tabbedPanel.ScheduleSnapshot;
        if (index < 0 || index >= snapshot.Count) return;

        var schedule = snapshot[index];
        _titleLabel.Text = $"Schedule {schedule.ScheduleId[..8]}";

        AddDetailRow("State", schedule.State.ToString());
        AddDetailRow("Origin", $"Continent {schedule.OriginContinentIndex}");
        AddDetailRow("Destination", FormatDestination(schedule.Destination));
        AddDetailRow("Mode", schedule.DepartureMode.ToString());
        AddDetailRow("Threshold", schedule.Threshold.ToString());
        if (!string.IsNullOrEmpty(schedule.ActiveTransferOrderId))
            AddDetailRow("Active Order", schedule.ActiveTransferOrderId[..8]);

        if (schedule.ResourceProportions.Count > 0)
        {
            AddSeparator();
            AddDetailHeader("Proportions");
            foreach (var kv in schedule.ResourceProportions.OrderByDescending(k => k.Value))
                AddDetailRow(FormatResourceLabel(kv.Key), $"{kv.Value * 100f:F0}%");
        }
    }

    private void RenderManifest(string headerText, CargoManifest manifest)
    {
        if (manifest.ResourceCount == 0) return;

        AddSeparator();
        AddDetailHeader(headerText);
        foreach (var kv in manifest.Resources.OrderByDescending(k => k.Value))
            AddDetailRow(FormatResourceLabel(kv.Key), $"{kv.Value:F1}");
    }

    // ───────── Helpers ─────────

    private ContinentEconomy.BuildingRegistration? FindRegistration(int buildingInstanceId)
    {
        var economy = _continent?.Economy;
        if (economy == null) return null;
        foreach (var reg in economy.ActiveBuildings)
        {
            if (reg.BuildingInstanceId == buildingInstanceId)
                return reg;
        }
        return null;
    }

    private static string FormatDestination(TransferDestination dest)
    {
        if (dest.IsOrbitalStation)
            return $"Station {dest.StationSatelliteId?[..8]}";
        return $"Continent {dest.ContinentIndex}";
    }

    private static string GetResourceCategory(string resourceId)
    {
        if (ResourceDatabase.Instance != null
            && ResourceDatabase.Instance.TryGetResource(resourceId, out var def)
            && def?.ResourceType != null)
        {
            return def.ResourceType;
        }
        if (resourceId.EndsWith("_ore")) return "ore";
        return "raw_material";
    }

    private static string FormatCategoryName(string category)
    {
        if (string.IsNullOrEmpty(category)) return "Other";
        return char.ToUpperInvariant(category[0]) + category.Substring(1);
    }

    private static string FormatResourceLabel(string resourceId)
    {
        if (string.IsNullOrEmpty(resourceId)) return "Unknown";
        var parts = resourceId.Split('_');
        return string.Join(
            " ",
            parts.Select(p => string.IsNullOrEmpty(p)
                ? p
                : char.ToUpperInvariant(p[0]) + p.Substring(1)));
    }

    private void ClearContent() => DetailRowBuilder.Clear(_contentContainer);

    private void AddDetailRow(string key, string value)
        => DetailRowBuilder.AddRow(_contentContainer, key, value);

    private void AddPercentRow(string key, float current, float max, string? unit = null,
        DonutChart.ColorMode mode = DonutChart.ColorMode.GreenToRed)
        => DetailRowBuilder.AddPercentRow(_contentContainer, key, current, max, unit, "F1", mode);

    private void AddPercentRow(string key, int current, int max,
        DonutChart.ColorMode mode = DonutChart.ColorMode.RedToGreen)
        => DetailRowBuilder.AddPercentRow(_contentContainer, key, current, max, mode);

    private void AddProgressRow(string key, float ratio,
        DonutChart.ColorMode mode = DonutChart.ColorMode.RedToGreen)
        => DetailRowBuilder.AddProgressRow(_contentContainer, key, ratio, mode);

    private void AddDetailHeader(string text)
        => DetailRowBuilder.AddHeader(_contentContainer, text);

    private void AddAlertRow(string message)
        => DetailRowBuilder.AddAlert(_contentContainer, message);

    private void AddSeparator()
        => DetailRowBuilder.AddSeparator(_contentContainer);
}
