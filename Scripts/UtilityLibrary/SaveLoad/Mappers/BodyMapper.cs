using System;
using System.Globalization;
using Godot;
using ProceduralGeneration.MeshGeneration;
using ProceduralGeneration.PlanetGeneration;
using Structures;
using Structures.Enums;
using Structures.GameState;
using Structures.MeshGeneration;
using Structures.Resources;
using UtilityLibrary.SaveLoad.Dto;

namespace UtilityLibrary.SaveLoad.Mappers;

/// <summary>
/// Maps CelestialBody / SatelliteBody runtime nodes to <see cref="BodyDto"/> and back. Geometry is
/// delegated to <see cref="GeometryMapper"/>. Tree placement, local Position, orbit-system init and
/// band-count restore are driven by the loader (see <see cref="ApplyPostInit"/>) because they depend
/// on the body already being in the scene tree.
/// </summary>
public static class BodyMapper
{
    // ---------- Save ----------

    public static BodyDto ToDto(CelestialBody body, string? parentName)
    {
        var dto = new BodyDto
        {
            Kind = "Celestial",
            Name = body.Name,
            Classification = body.Classification?.TypeName ?? "",
            Mass = body.Mass,
            Radius = body.Radius,
            BodySeed = body.BodySeed.ToString(CultureInfo.InvariantCulture),
            Atmosphere = body.Atmosphere,
            Position = Vec3Dto.From(body.Position),
            Velocity = Vec3Dto.From(body.Velocity),
            TotalForce = Vec3Dto.From(body.TotalForce),
            SavedForce = Vec3Dto.From(body.SavedForce),
            OrbitalParentName = parentName,
            Geometry = GeometryMapper.ToDto(body.Mesh),
        };

        foreach (var kv in body.BandCountsSnapshot)
            dto.BandSatelliteCounts.Add(new KvIntIntDto { Key = kv.Key, Value = kv.Value });

        return dto;
    }

    public static BodyDto ToDto(SatelliteBody body, string parentName)
    {
        var orbit = body.OrbitStateSnapshot;
        var dto = new BodyDto
        {
            Kind = "Satellite",
            Name = body.Name,
            Classification = body.Classification?.TypeName ?? "",
            Mass = body.Mass,
            Radius = body.Radius,
            BodySeed = "0",
            Atmosphere = 0f,
            Position = Vec3Dto.From(body.Position),
            Velocity = Vec3Dto.From(body.Velocity),
            OrbitalParentName = parentName,
            OrbitalAngle = orbit.Angle,
            OrbitalRadius = orbit.Radius,
            OrbitalSpeed = orbit.Speed,
            OrbitalInitialized = orbit.Initialized,
            Geometry = GeometryMapper.ToDto(body.Mesh),
        };

        foreach (var kv in body.BandCountsSnapshot)
            dto.BandSatelliteCounts.Add(new KvIntIntDto { Key = kv.Key, Value = kv.Value });

        if (body.Resources != null && body.Resources.Count > 0)
        {
            dto.SatelliteResources = new System.Collections.Generic.List<ResourceDepositDto>();
            foreach (var kv in body.Resources)
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

    // ---------- Load ----------

    /// <summary>
    /// Builds a body node from a DTO with its geometry restored. Does NOT add the node to the tree,
    /// set its Position, run InitializeOrbitSystem, or restore band counts — the loader does those
    /// once the node is parented. Returns the constructed Node3D (CelestialBody or SatelliteBody).
    /// </summary>
    public static Node3D Restore(BodyDto dto)
    {
        return dto.Kind == "Satellite" ? RestoreSatellite(dto) : RestoreCelestial(dto);
    }

    private static CelestialBody RestoreCelestial(BodyDto dto)
    {
        var mesh = new UnifiedCelestialMesh();
        var (strDb, oct) = GeometryMapper.Restore(mesh, dto.Geometry);

        var classification = ParseCelestialClassification(dto.Classification);
        var body = new CelestialBody.Builder()
            .WithMesh(mesh)
            .WithMass(dto.Mass)
            .WithVelocity(dto.Velocity.ToVector3())
            .WithClassification(classification)
            .WithName(dto.Name)
            .Build();

        body.AttachRestoredGeometry(strDb, oct);
        body.Radius = dto.Radius;
        body.Atmosphere = dto.Atmosphere;
        body.BodySeed = ParseSeed(dto.BodySeed);
        body.TotalForce = dto.TotalForce.ToVector3();
        body.SavedForce = dto.SavedForce.ToVector3();
        return body;
    }

    private static SatelliteBody RestoreSatellite(BodyDto dto)
    {
        var mesh = new UnifiedCelestialMesh();
        var (strDb, oct) = GeometryMapper.Restore(mesh, dto.Geometry);

        var satType = ParseSatelliteType(dto.Classification);
        var body = new SatelliteBody.Builder()
            .WithMesh(mesh)
            .WithMass(dto.Mass)
            .WithSize(dto.Radius)
            .WithVelocity(dto.Velocity.ToVector3())
            .WithSatelliteType(satType)
            .Build();
        body.Name = dto.Name;

        body.AttachRestoredGeometry(strDb, oct);
        body.Radius = dto.Radius;
        body.RestoreOrbitState(dto.OrbitalAngle, dto.OrbitalRadius, dto.OrbitalSpeed, dto.OrbitalInitialized);

        if (dto.SatelliteResources != null)
        {
            body.Resources = new System.Collections.Generic.Dictionary<string, ResourceDeposit>();
            foreach (var rd in dto.SatelliteResources)
                body.Resources[rd.ResourceId] = new ResourceDeposit(rd.ResourceId, rd.Abundance, rd.Accessibility);
        }

        return body;
    }

    /// <summary>
    /// Restores per-band occupancy counts. Call AFTER InitializeOrbitSystem (which rebuilds the bands
    /// and resets the counts to zero).
    /// </summary>
    public static void ApplyPostInit(Node3D node, BodyDto dto)
    {
        switch (node)
        {
            case CelestialBody cb:
                foreach (var kv in dto.BandSatelliteCounts)
                    cb.SetBandCount(kv.Key, kv.Value);
                break;
            case SatelliteBody sb:
                foreach (var kv in dto.BandSatelliteCounts)
                    sb.SetBandCount(kv.Key, kv.Value);
                break;
        }
    }

    private static BodyClassification ParseCelestialClassification(string typeName)
    {
        if (Enum.TryParse<CelestialBodyType>(typeName, out var cbt))
            return BodyClassification.FromLegacy(cbt, null);
        GameLogger.Warning($"[BodyMapper] Unknown celestial classification '{typeName}', defaulting to RockyPlanet.");
        return BodyClassification.FromLegacy(CelestialBodyType.RockyPlanet, null);
    }

    private static SatelliteBodyType ParseSatelliteType(string typeName)
    {
        if (Enum.TryParse<SatelliteBodyType>(typeName, out var st))
            return st;
        GameLogger.Warning($"[BodyMapper] Unknown satellite classification '{typeName}', defaulting to Asteroid.");
        return SatelliteBodyType.Asteroid;
    }

    private static ulong ParseSeed(string s) =>
        ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0UL;
}
