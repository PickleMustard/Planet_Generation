using Godot;

namespace UI.StationWindow;

/// <summary>
/// Thin wrapper that hosts the existing rich <see cref="ShipyardPanel"/> as a station behavior tab.
/// Keeps the tab loop uniform (every tab is a <see cref="StationDetailPanel"/>) while leaving
/// <see cref="ShipyardPanel"/>'s own self-polling and <c>SignalBus</c> sidebar flow untouched.
/// The hosted <see cref="ShipyardPanel"/> is an instanced child wired in
/// StationShipyardPanel.tscn via the <see cref="_inner"/> export.
/// </summary>
public partial class StationShipyardPanel : StationDetailPanel
{
    [Export]
    private ShipyardPanel? _inner;

    public override void _Ready()
    {
        // Layout lives in StationShipyardPanel.tscn; this only handles the case where
        // SetStation ran before the node entered the tree.
        if (_station != null)
            UpdateDisplay();
    }

    protected override void UpdateDisplay()
    {
        if (_inner != null && _station != null)
            _inner.Bind(_station);
    }

    public override void Clear()
    {
        _inner?.Clear();
        base.Clear();
    }
}
