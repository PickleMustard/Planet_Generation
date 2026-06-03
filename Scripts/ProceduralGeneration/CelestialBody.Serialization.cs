using System.Globalization;
using Godot;
using Structures;
using Structures.Resources;
using UtilityLibrary.SaveLoad;
using UtilityLibrary.SaveLoad.Dto;
using UtilityLibrary.SaveLoad.Mappers;

namespace ProceduralGeneration.PlanetGeneration;

/// <summary>
/// Save side of <see cref="CelestialBody"/> (covers planets and their satellite/belt descendants, which
/// are also CelestialBody instances). Builds a <see cref="BodyDto"/>; geometry is delegated to
/// <see cref="GeometryMapper"/>. The load path stays in <c>BodyMapper.Restore</c> + the loader's ordered
/// passes — a body cannot instantiate its own subtype, set its tree position, or resolve parent links.
/// Discovered by the saver through the system_container tree walk, not the "save_serializable" group.
/// </summary>
public partial class CelestialBody : ISaveSerializable
{
    /// <summary>Section discriminator (the saver routes bodies via the tree walk, not by key).</summary>
    public string SaveKey => "body";

    /// <summary>Snapshots this body into a <see cref="BodyDto"/>. See <see cref="ISaveSerializable"/>.</summary>
    public object Serialize()
    {
        bool isSatellite = Classification
            is BodyClassification.Satellite or BodyClassification.Belt;
        var orbit = OrbitStateSnapshot;
        var dto = new BodyDto
        {
            // Kind drives the loader's two-pass parent-after-child ordering; derive it from the
            // classification now that satellites are also CelestialBody instances.
            Kind = isSatellite ? "Satellite" : "Celestial",
            Name = Name,
            Classification = Classification?.TypeName ?? "",
            Mass = Mass,
            Radius = Radius,
            BodySeed = BodySeed.ToString(CultureInfo.InvariantCulture),
            Atmosphere = Atmosphere,
            Position = Vec3Dto.From(Position),
            Velocity = Vec3Dto.From(Velocity),
            TotalForce = Vec3Dto.From(TotalForce),
            SavedForce = Vec3Dto.From(SavedForce),
            OrbitalParentName = DeriveOrbitalParentName(isSatellite),
            OrbitalAngle = orbit.Angle,
            OrbitalRadius = orbit.Radius,
            OrbitalSpeed = orbit.Speed,
            OrbitalInitialized = orbit.Initialized,
            Geometry = GeometryMapper.ToDto(Mesh),
        };

        foreach (var kv in BandCountsSnapshot)
            dto.BandSatelliteCounts.Add(new KvIntIntDto { Key = kv.Key, Value = kv.Value });

        if (Resources != null && Resources.Count > 0)
        {
            dto.SatelliteResources = new System.Collections.Generic.List<ResourceDepositDto>();
            foreach (var kv in Resources)
            {
                dto.SatelliteResources.Add(new ResourceDepositDto
                {
                    ResourceId = kv.Value.ResourceId,
                    Abundance = kv.Value.Abundance,
                    Accessibility = kv.Value.Accessibility,
                });
            }
        }

        return dto;
    }

    /// <summary>
    /// Reproduces the saved <c>OrbitalParentName</c>: a top-level celestial body uses its
    /// <see cref="OrbitalParent"/>'s node name (a name reference resolved in loader pass 3); a satellite
    /// uses its top-level CelestialBody ancestor (the direct system_container child whose subtree it sits
    /// in). All satellites — including moon-of-moon chains — are saved/loaded flat under that host, so the
    /// topmost ancestor, not the immediate <see cref="OrbitalParent"/>, is the correct reference.
    /// </summary>
    private string? DeriveOrbitalParentName(bool isSatellite)
    {
        if (!isSatellite)
            return (OrbitalParent as Node)?.Name;

        CelestialBody? host = null;
        for (Node? p = GetParent(); p != null; p = p.GetParent())
            if (p is CelestialBody cb)
                host = cb;
        return host?.Name;
    }
}
