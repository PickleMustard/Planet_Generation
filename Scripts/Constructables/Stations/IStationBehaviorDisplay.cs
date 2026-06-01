using System.Collections.Generic;

namespace Constructables.Stations;

/// <summary>
/// Opt-in display contract for a station behavior. A behavior implementing this is rendered
/// generically by <c>UI.StationWindow.StationGenericPanel</c>: its <see cref="GetDisplayRows"/> become
/// key/value rows and, when <see cref="ShowStorageGrid"/> is true, the panel also renders the owning
/// station's bulk-storage slot grid. New simple behaviors get a tab with zero bespoke UI work — only
/// rich behaviors (shipyard, architect) and the dual-source transfer tab need dedicated panels.
/// </summary>
public interface IStationBehaviorDisplay
{
    /// <summary>Tab label shown for this behavior.</summary>
    string TabLabel { get; }

    /// <summary>Key/value rows describing the behavior's current state.</summary>
    IEnumerable<(string Key, string Value)> GetDisplayRows();

    /// <summary>When true, the generic panel also renders the station's bulk-storage slot grid.</summary>
    bool ShowStorageGrid => false;
}
