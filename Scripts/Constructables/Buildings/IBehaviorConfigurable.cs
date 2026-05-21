using System.Collections.Generic;

namespace Constructables.Buildings;

/// <summary>
/// Marks a behavior as accepting YAML-driven configuration. The loader
/// calls Configure() after instantiation but before OnAttach(), giving
/// the behavior a chance to read its inline config dict from the
/// behaviors: section of the building YAML.
/// Adding a new configurable behavior only requires implementing this
/// interface — no loader or factory changes needed.
/// </summary>
public interface IBehaviorConfigurable
{
    void Configure(Dictionary<string, object> config);
}
