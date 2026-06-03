using System.Linq;
using Godot;
using ProceduralGeneration.PlanetGeneration;
using Constructables.Stations.Behaviors;
using UtilityLibrary.SaveLoad;
using UtilityLibrary.SaveLoad.Dto;
using UtilityLibrary.SaveLoad.Mappers;

namespace Constructables;

/// <summary>
/// Save side of <see cref="StationSatellite"/> (save_version 3). Builds a <see cref="StationDto"/>
/// capturing orbital arc, bulk storage and transfer-hub state; the host body name is self-derived from
/// <see cref="ParentBody"/>. The load path stays in <c>StationMapper.Restore</c> — a station cannot
/// rebuild itself from its <see cref="StationDefinition"/>, re-parent under the host's container, or
/// resolve its body reference by name. Discovered by the saver via the host's SatellitesContainer walk,
/// not the "save_serializable" group.
/// </summary>
public partial class StationSatellite : ISaveSerializable
{
    /// <summary>Section discriminator (the saver routes stations via the tree walk, not by key).</summary>
    public string SaveKey => "station";

    /// <summary>Snapshots this station into a <see cref="StationDto"/>. See <see cref="ISaveSerializable"/>.</summary>
    public object Serialize()
    {
        var dto = new StationDto
        {
            Id = Id,
            Name = Name,
            DefinitionName = Definition?.Name ?? "",
            BodyName = FindBodyName() ?? "",
            BandIndex = BandIndex,
            IsActive = IsActive,
            IsStationary = IsStationary,
            OrbitalAngle = OrbitalAngle,
            OrbitalRadius = OrbitalRadius,
            OrbitalSpeed = OrbitalSpeed,
            HostMass = HostMass,
            Position = Vec3Dto.From(Position),
            Velocity = Vec3Dto.From(Velocity),
        };

        foreach (var kv in BulkStorage.GetAllQuantities())
            dto.BulkStorage[kv.Key] = kv.Value;

        var hub = GetBehavior<TransferHubBehavior>();
        if (hub != null)
            dto.Transfer = TransferMapper.ToDto(
                hub.TotalTime,
                hub.GetActiveTransfers().Values.Select(t => t.Order),
                hub.GetAllSchedules());

        var market = GetBehavior<MarketStationBehavior>();
        if (market != null)
            dto.MarketState = market.SerializeState();

        return dto;
    }

    /// <summary>Resolves the owning body's node name from <see cref="ParentBody"/>, falling back to the
    /// nearest IOrbitalBody ancestor in the scene tree (the saver's tree-derived host).</summary>
    private string? FindBodyName()
    {
        if (ParentBody is Node pn)
            return pn.Name;
        for (Node? p = GetParent(); p != null; p = p.GetParent())
            if (p is CelestialBody cb)
                return cb.Name;
        return null;
    }
}
