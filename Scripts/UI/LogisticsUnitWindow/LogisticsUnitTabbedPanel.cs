using System.Collections.Generic;
using Constructables;
using Godot;
using Structures.Enums;
using Structures.Logistics;

namespace UI.LogisticsUnitWindow;

/// <summary>
/// Right-side tabbed panel with General Info, Cargo Manifest, and Route Information tabs.
/// Switching tabs populates the content area; clicking items emits ItemSelected.
/// </summary>
public partial class LogisticsUnitTabbedPanel : PanelContainer
{
    [Signal]
    public delegate void ItemSelectedEventHandler(string itemType, int itemIndex);

    private enum UnitTab
    {
        GeneralInfo = 0,
        CargoManifest = 1,
        RouteInfo = 2,
    }

    private struct TabEntry
    {
        public UnitTab Tab;
        public Button Button;
    }

    private LogisticsUnit? _unit;
    private int _activeTabIndex = -1;
    private readonly List<TabEntry> _visibleTabs = new();
    private HBoxContainer? _tabBar;
    private VBoxContainer? _contentContainer;

    private StyleBox? _activeTabStyle;
    private StyleBox? _inactiveTabStyle;

    public override void _Ready()
    {
        _tabBar = GetNode<HBoxContainer>("VBoxContainer/TabBar");
        _contentContainer = GetNode<VBoxContainer>(
            "VBoxContainer/TabContent/ContentContainer"
        );
    }

    public void Initialize(LogisticsUnit unit)
    {
        _unit = unit;
        RebuildTabs();
        if (_visibleTabs.Count > 0)
            SwitchTab(0);
    }

    public void Clear()
    {
        _unit = null;
        ClearContent();
        ClearTabs();
        _activeTabIndex = -1;
    }

    // ───────── Tab Management ─────────

    private void RebuildTabs()
    {
        ClearTabs();

        if (_unit == null || _tabBar == null)
            return;

        var allTabs = new[]
        {
            UnitTab.GeneralInfo,
            UnitTab.CargoManifest,
            UnitTab.RouteInfo,
        };

        bool isFirst = true;
        foreach (var tab in allTabs)
        {
            var button = new Button
            {
                Text = GetTabName(tab),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            button.AddThemeFontSizeOverride("font_size", 13);

            if (isFirst)
            {
                _activeTabStyle = new StyleBoxFlat
                {
                    ContentMarginLeft = 8,
                    ContentMarginTop = 4,
                    ContentMarginRight = 8,
                    ContentMarginBottom = 4,
                    BgColor = new Color(0.25f, 0.25f, 0.3f, 1f),
                    CornerRadiusTopLeft = 4,
                    CornerRadiusTopRight = 4,
                };
                _inactiveTabStyle = new StyleBoxFlat
                {
                    ContentMarginLeft = 8,
                    ContentMarginTop = 4,
                    ContentMarginRight = 8,
                    ContentMarginBottom = 4,
                    BgColor = new Color(0.12f, 0.12f, 0.14f, 0.8f),
                    CornerRadiusTopLeft = 4,
                    CornerRadiusTopRight = 4,
                };
                isFirst = false;
            }

            int visibleIndex = _visibleTabs.Count;
            button.Pressed += () => SwitchTab(visibleIndex);

            _tabBar.AddChild(button);
            _visibleTabs.Add(new TabEntry { Tab = tab, Button = button });
        }
    }

    private void ClearTabs()
    {
        if (_tabBar != null)
        {
            foreach (var child in _tabBar.GetChildren())
                child.QueueFree();
        }
        _visibleTabs.Clear();
    }

    private static string GetTabName(UnitTab tab)
    {
        return tab switch
        {
            UnitTab.GeneralInfo => "General",
            UnitTab.CargoManifest => "Cargo",
            UnitTab.RouteInfo => "Route",
            _ => tab.ToString(),
        };
    }

    public void SwitchTab(int visibleTabIndex)
    {
        if (visibleTabIndex < 0 || visibleTabIndex >= _visibleTabs.Count)
            return;

        if (visibleTabIndex == _activeTabIndex)
            return;

        _activeTabIndex = visibleTabIndex;
        UpdateTabButtonStyles();
        ClearContent();

        if (_unit == null || _contentContainer == null)
            return;

        var tab = _visibleTabs[visibleTabIndex].Tab;
        switch (tab)
        {
            case UnitTab.GeneralInfo:
                PopulateGeneralInfo();
                break;
            case UnitTab.CargoManifest:
                PopulateCargoManifest();
                break;
            case UnitTab.RouteInfo:
                PopulateRouteInfo();
                break;
        }
    }

    private void UpdateTabButtonStyles()
    {
        if (_activeTabStyle == null || _inactiveTabStyle == null)
            return;

        for (int i = 0; i < _visibleTabs.Count; i++)
        {
            _visibleTabs[i].Button.AddThemeStyleboxOverride(
                "normal",
                i == _activeTabIndex ? _activeTabStyle : _inactiveTabStyle
            );
        }
    }

    private void ClearContent()
    {
        if (_contentContainer == null)
            return;
        foreach (var child in _contentContainer.GetChildren())
            child.QueueFree();
    }

    // ───────── General Info Tab ─────────

    private void PopulateGeneralInfo()
    {
        if (_unit == null || _contentContainer == null)
            return;

        AddSectionHeader("Identity");
        AddInfoRow("Name", _unit.Name);
        AddInfoRow("Type", _unit.ShipDef?.Name ?? "Unknown");
        AddInfoRow("State", _unit.State.ToString());

        AddSectionHeader("Mass");
        AddInfoRow("Dry Mass", $"{_unit.DryMass:F1} kg");
        AddInfoRow("Cargo Mass", $"{_unit.GetCargoMass():F1} kg");
        AddInfoRow("Fuel", $"{_unit.Fuel:F1} / {_unit.MaxFuel:F1} kg");
        AddInfoRow("Total Mass", $"{_unit.GetTotalMass():F1} kg");

        if (_unit.ShipDef != null)
        {
            AddInfoRow("Cargo Capacity", $"{_unit.ShipDef.CargoCapacity:F1} kg");
        }

        // Engine info
        var engine = _unit.CurrentEngine;
        if (engine != null)
        {
            AddSectionHeader("Engine");
            var row = CreateClickableRow(0, "engine");
            var label = new Label
            {
                Text = $"Isp: {engine.EffectiveSpecificImpulse:F1}s  |  Thrust: {engine.EffectiveThrust:F1}N",
            };
            label.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
            label.AddThemeFontSizeOverride("font_size", 13);
            row.AddChild(label);
            _contentContainer.AddChild(row);

            AddInfoRow("Exhaust Velocity", $"{engine.ExhaustVelocity:F1} m/s");
        }

        AddSectionHeader("Delta-V");
        AddInfoRow("Available", $"{_unit.GetAvailableDeltaV():F1} m/s");

        AddSectionHeader("Orbital Parameters");
        AddInfoRow("Radius", $"{_unit.OrbitalRadius:F2}");
        AddInfoRow("Speed", $"{_unit.OrbitalSpeed:F6} rad/s");
        AddInfoRow("Band", _unit.BandIndex >= 0 ? _unit.BandIndex.ToString() : "Continuous");
    }

    // ───────── Cargo Manifest Tab ─────────

    private void PopulateCargoManifest()
    {
        if (_unit == null || _contentContainer == null)
            return;

        var cargo = _unit.Cargo;
        if (cargo == null || cargo.Resources.Count == 0)
        {
            AddEmptyLabel("No cargo loaded");
            return;
        }

        AddSectionHeader("Cargo Manifest");

        int index = 0;
        foreach (var kvp in cargo.Resources)
        {
            if (kvp.Value <= 0)
                continue;

            var row = CreateClickableRow(index, "resource");

            var label = new Label
            {
                Text = $"{kvp.Key}:  {kvp.Value:F1}",
            };
            label.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f));
            label.AddThemeFontSizeOverride("font_size", 13);
            row.AddChild(label);

            _contentContainer.AddChild(row);
            index++;
        }

        AddSectionHeader("Summary");
        AddInfoRow("Total Mass", $"{cargo.TotalCargoMass:F1} kg");
        AddInfoRow("Resource Types", cargo.Resources.Count.ToString());

        if (_unit.ShipDef != null && _unit.ShipDef.CargoCapacity > 0)
        {
            float usagePct = (cargo.TotalCargoMass / _unit.ShipDef.CargoCapacity) * 100f;
            AddInfoRow("Capacity Used", $"{usagePct:F0}%");
        }
    }

    // ───────── Route Info Tab ─────────

    private void PopulateRouteInfo()
    {
        if (_unit == null || _contentContainer == null)
            return;

        var schedule = _unit.ScheduleExecutor?.ActiveSchedule;
        if (schedule == null)
        {
            AddEmptyLabel("No active schedule");
            return;
        }

        AddSectionHeader("Schedule");
        AddInfoRow("State", schedule.State.ToString());
        AddInfoRow("Repeating", schedule.IsRepeating ? "Yes" : "No");
        AddInfoRow("Legs", schedule.Legs.Count.ToString());

        if (schedule.Legs.Count > 0)
        {
            AddSectionHeader("Legs");
            for (int i = 0; i < schedule.Legs.Count; i++)
            {
                var leg = schedule.Legs[i];
                var row = CreateClickableRow(i, "leg");

                string originName = leg.Origin?.Station != null
                    ? leg.Origin.Station.Name
                    : leg.Origin?.Body?.BodyName ?? "?";
                string destName = leg.Destination?.Station != null
                    ? leg.Destination.Station.Name
                    : leg.Destination?.Body?.BodyName ?? "?";

                bool isCurrent = i == schedule.CurrentLegIndex
                    && schedule.State == OrbitalScheduleState.Running;

                string prefix = isCurrent ? "> " : "  ";
                var label = new Label
                {
                    Text = $"{prefix}{originName} -> {destName}  [{leg.State}]",
                };

                var fontColor = isCurrent
                    ? new Color(0.5f, 1f, 0.5f)
                    : new Color(0.85f, 0.85f, 0.9f);
                label.AddThemeColorOverride("font_color", fontColor);
                label.AddThemeFontSizeOverride("font_size", 13);
                row.AddChild(label);

                _contentContainer.AddChild(row);
            }
        }

        // Current trajectory info
        var currentLeg = schedule.CurrentLeg;
        if (currentLeg?.SelectedTrajectory != null)
        {
            var traj = currentLeg.SelectedTrajectory;
            AddSectionHeader("Current Trajectory");
            AddInfoRow("Delta-V", $"{traj.DeltaVRequired:F1} m/s");
            AddInfoRow("Time of Flight", $"{traj.TimeOfFlight:F1} s");
            AddInfoRow("Transfer Type", traj.TransferType.ToString());

            if (_unit.MovementController != null && _unit.MovementController.IsTransferring)
            {
                AddInfoRow("Progress", $"{_unit.MovementController.TransferProgress * 100:F0}%");
            }
        }
    }

    // ───────── Helpers ─────────

    private HBoxContainer CreateClickableRow(int index, string itemType)
    {
        var row = new HBoxContainer();
        row.MouseFilter = MouseFilterEnum.Stop;
        row.AddThemeConstantOverride("separation", 4);

        row.GuiInput += (InputEvent @event) =>
        {
            if (
                @event is InputEventMouseButton btn
                && btn.ButtonIndex == MouseButton.Left
                && btn.Pressed
            )
            {
                EmitSignal(SignalName.ItemSelected, itemType, index);
                row.GetViewport().SetInputAsHandled();
            }
        };

        return row;
    }

    private void AddSectionHeader(string text)
    {
        if (_contentContainer == null)
            return;

        var header = new Label { Text = text };
        header.AddThemeColorOverride("font_color", new Color(0.7f, 0.8f, 0.95f));
        header.AddThemeFontSizeOverride("font_size", 15);
        _contentContainer.AddChild(header);

        _contentContainer.AddChild(new HSeparator());
    }

    private void AddInfoRow(string labelText, string value)
    {
        if (_contentContainer == null)
            return;

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 12);

        var keyLabel = new Label { Text = labelText + ":" };
        keyLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f));
        keyLabel.AddThemeFontSizeOverride("font_size", 13);
        keyLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(keyLabel);

        var valLabel = new Label { Text = value };
        valLabel.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(valLabel);

        _contentContainer.AddChild(row);
    }

    private void AddEmptyLabel(string text)
    {
        if (_contentContainer == null)
            return;

        var label = new Label { Text = text };
        label.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
        label.AddThemeFontSizeOverride("font_size", 13);
        _contentContainer.AddChild(label);
    }
}
