using System.Collections.Generic;
using Constructables;
using Godot;
using Structures.Logistics;
using UI.Components;

namespace UI.LogisticsUnitWindow;

/// <summary>
/// Tab strip for the logistics-unit details window. Three tabs — Overview,
/// Route Planning, Manifest — each exposing a row of sub-buttons. Selecting a
/// sub-button emits <see cref="SectionSelected"/> with a section key that
/// <see cref="LogisticsUnitInfoPanel"/> resolves into a column layout. The
/// active tab auto-selects its first sub-button.
/// </summary>
public partial class LogisticsUnitTabbedPanel : PanelContainer
{
    [Signal]
    public delegate void SectionSelectedEventHandler(string sectionKey);

    private enum UnitTab { Overview, Route, Manifest }

    private LogisticsUnit? _unit;
    private HBoxContainer? _tabBar;
    private HBoxContainer? _subButtonBar;

    private readonly List<(UnitTab Tab, Button Button)> _tabButtons = new();
    private readonly List<Button> _subButtons = new();
    private UnitTab _activeTab = UnitTab.Overview;
    private string _activeSection = "";

    public override void _Ready()
    {
        _tabBar = GetNodeOrNull<HBoxContainer>("VBoxContainer/TabBar");
        _subButtonBar = GetNodeOrNull<HBoxContainer>("VBoxContainer/SubButtonBar");
    }

    public void Initialize(LogisticsUnit unit)
    {
        _unit = unit;
        BuildTabs();
        SwitchTab(UnitTab.Overview);
    }

    public void Clear()
    {
        _unit = null;
        ClearChildren(_tabBar);
        ClearChildren(_subButtonBar);
        _tabButtons.Clear();
        _subButtons.Clear();
        _activeSection = "";
    }

    // ───────── Tabs ─────────

    private void BuildTabs()
    {
        ClearChildren(_tabBar);
        _tabButtons.Clear();

        if (_tabBar == null)
            return;

        AddTabButton(UnitTab.Overview, "Overview");
        AddTabButton(UnitTab.Route, "Route Planning");
        AddTabButton(UnitTab.Manifest, "Manifest");
    }

    private void AddTabButton(UnitTab tab, string label)
    {
        var button = new Button
        {
            Text = label,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ThemeTypeVariation = "TabInactive",
        };
        button.Pressed += () => SwitchTab(tab);
        _tabBar!.AddChild(button);
        _tabButtons.Add((tab, button));
    }

    private void SwitchTab(UnitTab tab)
    {
        _activeTab = tab;

        foreach (var (t, b) in _tabButtons)
            b.ThemeTypeVariation = t == tab ? "TabActive" : "TabInactive";

        RebuildSubButtons();
    }

    // ───────── Sub-buttons ─────────

    private void RebuildSubButtons()
    {
        ClearChildren(_subButtonBar);
        _subButtons.Clear();

        if (_subButtonBar == null || _unit == null)
            return;

        switch (_activeTab)
        {
            case UnitTab.Overview:
                AddSubButton("General Info", "general");
                AddSubButton("Statistics", "statistics");
                AddSubButton("Engine", "engine");
                AddSubButton("Upgrades", "upgrades");
                break;

            case UnitTab.Route:
                BuildRouteSubButtons();
                break;

            case UnitTab.Manifest:
                BuildManifestSubButtons();
                break;
        }

        // Auto-select the first available sub-button.
        if (_subButtons.Count > 0)
            SelectSection(_subButtons[0].GetMeta("section_key").AsString());
        else
            SelectSection("");
    }

    private void BuildRouteSubButtons()
    {
        var schedule = LogisticsUnitDataHelpers.GetSchedule(_unit!);
        if (schedule == null || schedule.Legs.Count == 0)
            return; // info panel shows the "no route" empty state for "route:none"

        int current = schedule.CurrentLegIndex;
        int count = schedule.Legs.Count;

        // Order so the next / in-progress leg is first, then the rest in order,
        // wrapping completed legs to the end.
        for (int offset = 0; offset < count; offset++)
        {
            int idx = current >= 0 ? (current + offset) % count : offset;
            var leg = schedule.Legs[idx];
            string origin = LogisticsUnitDataHelpers.EndpointName(leg.Origin);
            string dest = LogisticsUnitDataHelpers.EndpointName(leg.Destination);
            bool isCurrent = idx == current && schedule.State == Structures.Enums.OrbitalScheduleState.Running;
            string label = $"{(isCurrent ? "▶ " : "")}{origin}→{dest}";
            AddSubButton(label, $"leg:{idx}");
        }
    }

    private void BuildManifestSubButtons()
    {
        // The unit currently exposes a single CargoManifest. The design supports
        // multiple holds; add one sub-button per hold here when that lands.
        AddSubButton("Cargo Hold", "hold:0");
    }

    private void AddSubButton(string label, string sectionKey)
    {
        var button = new Button { Text = label };
        button.SetMeta("section_key", sectionKey);
        button.Pressed += () => SelectSection(sectionKey);
        _subButtonBar!.AddChild(button);
        _subButtons.Add(button);
    }

    private void SelectSection(string sectionKey)
    {
        _activeSection = sectionKey;

        foreach (var b in _subButtons)
        {
            bool active = b.GetMeta("section_key").AsString() == sectionKey;
            b.ThemeTypeVariation = active ? "ButtonPrimary" : "";
        }

        string emitted = string.IsNullOrEmpty(sectionKey) && _activeTab == UnitTab.Route
            ? "route:none"
            : sectionKey;
        EmitSignal(SignalName.SectionSelected, emitted);
    }

    // ───────── Helpers ─────────

    private static void ClearChildren(Node? container)
    {
        if (container == null)
            return;
        foreach (var child in container.GetChildren())
            child.QueueFree();
    }
}
