using System.Collections.Generic;
using Constructables;
using ProceduralGeneration.PlanetGeneration;
using Structures.Resources;
using UtilityLibrary;

namespace Structures.Logistics;

/// <summary>
/// Programmatic façade for connecting and disconnecting <see cref="ResourceLink"/>s between
/// building <see cref="ResourceNode"/>s. Validates with <see cref="ResourceLink.CanConnect"/>,
/// instantiates the link, registers it with the <see cref="Constructables.Tick.ManufactureTickEngine"/>
/// (via <see cref="ResourceLink.ConnectNodes"/>), and tracks live links per body so UI can
/// query them. Precondition for the eventual breadboard GUI; usable today from gameplay code.
///
/// This service holds no game state of its own — links are owned by the nodes themselves and
/// the tick engine. The per-body registry is a convenience index for queries.
/// </summary>
public sealed class BuildingLinkService
{
    private static BuildingLinkService? _instance;
    public static BuildingLinkService Instance => _instance ??= new BuildingLinkService();

    private readonly Dictionary<CelestialBody, List<ResourceLink>> _linksByBody = new();
    private readonly object _lock = new();

    private BuildingLinkService() { }

    /// <summary>
    /// Resets the service. Called by SystemData on game start/end so a new session starts clean.
    /// </summary>
    public static void ResetForNewSession()
    {
        _instance = new BuildingLinkService();
    }

    /// <summary>
    /// Attempts to connect two nodes through a new link with the given profile.
    /// Returns false (and leaves <paramref name="link"/> null) when:
    ///   - either node is null,
    ///   - <see cref="ResourceLink.CanConnect"/> rejects the pair (same-kind import↔import etc.),
    ///   - the source already has a link,
    ///   - the target already has a link.
    /// </summary>
    public bool TryConnect(ResourceNode? a, ResourceNode? b, LinkProfile profile, out ResourceLink? link)
    {
        return TryConnect(a, b, profile, out link, out _);
    }

    /// <summary>
    /// Overload that exposes a human-readable rejection reason when the connection cannot be made.
    /// Used by the planning UI to surface feedback to the player.
    /// </summary>
    public bool TryConnect(
        ResourceNode? a,
        ResourceNode? b,
        LinkProfile profile,
        out ResourceLink? link,
        out string? rejectionReason)
    {
        link = null;
        rejectionReason = null;
        if (a == null || b == null || profile == null)
        {
            rejectionReason = "missing node or profile";
            return false;
        }
        if (!ResourceLink.CanConnect(a, b))
        {
            rejectionReason = "nodes are not compatible (same kind, or both Import / both Export)";
            return false;
        }
        if (a.Link != null || b.Link != null)
        {
            rejectionReason = "one of the nodes already has a link";
            return false;
        }
        if (!ResourceLink.CanCarry(a, profile.StateOfMatter, out var reasonA))
        {
            rejectionReason = reasonA;
            GameLogger.Warning($"Link rejected (source): {reasonA}");
            return false;
        }
        if (!ResourceLink.CanCarry(b, profile.StateOfMatter, out var reasonB))
        {
            rejectionReason = reasonB;
            GameLogger.Warning($"Link rejected (target): {reasonB}");
            return false;
        }

        // Orient so source is Export (or Flex), target is Import (or Flex).
        ResourceNode source, target;
        if (a.Kind == ResourceNodeKind.Import && b.Kind != ResourceNodeKind.Import)
            (source, target) = (b, a);
        else
            (source, target) = (a, b);

        var newLink = new ResourceLink { Profile = profile };
        newLink.ConnectNodes(source, target);

        var body = ResolveBody(a) ?? ResolveBody(b);
        if (body != null)
        {
            lock (_lock)
            {
                if (!_linksByBody.TryGetValue(body, out var list))
                {
                    list = new List<ResourceLink>();
                    _linksByBody[body] = list;
                }
                list.Add(newLink);
            }
        }

        link = newLink;
        return true;
    }

    /// <summary>
    /// Disconnects and unregisters the given link. Safe to call on a link that was never
    /// registered with the service.
    /// </summary>
    public void Disconnect(ResourceLink link)
    {
        if (link == null)
            return;

        var body = ResolveBody(link.Source) ?? ResolveBody(link.Target);
        if (body != null)
        {
            lock (_lock)
            {
                if (_linksByBody.TryGetValue(body, out var list))
                    list.Remove(link);
            }
        }

        link.Disconnect();
    }

    /// <summary>
    /// Returns a snapshot of all links currently tracked for the given body. Snapshot — safe
    /// to enumerate concurrently with further connect/disconnect calls.
    /// </summary>
    public IReadOnlyList<ResourceLink> GetLinksForBody(CelestialBody body)
    {
        lock (_lock)
        {
            if (!_linksByBody.TryGetValue(body, out var list))
                return System.Array.Empty<ResourceLink>();
            return list.ToArray();
        }
    }

    private static CelestialBody? ResolveBody(ResourceNode? node)
    {
        // Walk Owner.PrimaryCell → Continent → CelestialBody. Continent has no body backref;
        // for now, return null when unresolvable so callers fall through to the other endpoint.
        // This is best-effort tracking; correctness still rests on ResourceLink itself.
        _ = node?.Owner?.PrimaryCell; // placeholder until cell→body lookup is wired
        return null;
    }
}
