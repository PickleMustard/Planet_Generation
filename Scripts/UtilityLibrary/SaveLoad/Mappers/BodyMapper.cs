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
/// Load half for CelestialBody / satellite nodes: rebuilds a <see cref="BodyDto"/> into a live body with
/// its geometry restored (geometry delegated to <see cref="GeometryMapper"/>). Tree placement, local
/// Position, orbit-system init and band-count restore are driven by the loader (see
/// <see cref="ApplyPostInit"/>) because they depend on the body already being in the scene tree. The save
/// half lives on the body itself (<c>CelestialBody.Serialize()</c>).
/// </summary>
public static class BodyMapper
{
    // ---------- Load ----------

    /// <summary>
    /// Builds a CelestialBody node from a DTO with its geometry restored. Does NOT add the node to the
    /// tree, set its Position, run InitializeOrbitSystem, or restore band counts — the loader does
    /// those once the node is parented.
    /// </summary>
    public static Node3D Restore(BodyDto dto)
    {
        var mesh = new UnifiedCelestialMesh();
        var (strDb, oct) = GeometryMapper.Restore(mesh, dto.Geometry);

        bool isSatellite = dto.Kind == "Satellite";
        var classification = ParseClassification(dto.Classification);
        var builder = new CelestialBody.Builder()
            .WithMesh(mesh)
            .WithMass(dto.Mass)
            .WithVelocity(dto.Velocity.ToVector3())
            .WithClassification(classification)
            .WithName(dto.Name);
        if (isSatellite)
        {
            // Satellites integrate analytically (depth ≥ 2). Saves break across this refactor
            // (decision #6); depth is not persisted, so restore a depth that selects the analytical
            // branch rather than N-body.
            builder.WithSize(dto.Radius).WithDepth(2);
        }
        var body = builder.Build();

        body.AttachRestoredGeometry(strDb, oct);
        body.Radius = dto.Radius;
        body.Atmosphere = dto.Atmosphere;
        body.BodySeed = ParseSeed(dto.BodySeed);
        body.TotalForce = dto.TotalForce.ToVector3();
        body.SavedForce = dto.SavedForce.ToVector3();
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
        if (node is CelestialBody cb)
        {
            foreach (var kv in dto.BandSatelliteCounts)
                cb.SetBandCount(kv.Key, kv.Value);
        }
    }

    private static BodyClassification ParseClassification(string typeName)
    {
        if (Enum.TryParse<OrbitalBodyType>(typeName, out var cbt))
            return BodyClassification.FromType(cbt, null);
        GameLogger.Warning($"[BodyMapper] Unknown classification '{typeName}', defaulting to RockyPlanet.");
        return BodyClassification.FromType(OrbitalBodyType.RockyPlanet, null);
    }

    private static ulong ParseSeed(string s) =>
        ulong.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0UL;
}
