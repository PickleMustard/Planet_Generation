using System.Collections.Generic;

namespace Constructables.Stations;

/// <summary>
/// Marks a station behavior as accepting YAML-driven configuration. The loader
/// calls Configure() after instantiation but before OnAttach(), giving
/// the behavior a chance to read its inline config dict from the
/// behaviors: section of the station YAML.
/// Mirrors <see cref="Buildings.IBehaviorConfigurable"/> for station behaviors.
/// </summary>
public interface IStationBehaviorConfigurable
{
    void Configure(Dictionary<string, object> config);
}