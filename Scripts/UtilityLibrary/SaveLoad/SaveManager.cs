using System;
using System.Globalization;
using Constructables;
using Godot;
using ProceduralGeneration;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using UtilityLibrary.SaveLoad.Dto;
using UtilityLibrary.SaveLoad.Mappers;

namespace UtilityLibrary.SaveLoad;

/// <summary>
/// Entry point for writing a save file from the live scene. Walks the system_container tree,
/// snapshots celestial/satellite bodies + their geometry, company, economy and session state, and
/// serializes to user://saves. Manufacturing progress is intentionally not captured.
/// </summary>
public static class SaveManager
{
    /// <summary>
    /// Saves the current session. <paramref name="systemContainer"/> is the node holding the
    /// SystemData/CompanyDataTracker/EconomyTracker siblings and the CelestialBody children. When
    /// <paramref name="path"/> is null a timestamped file under user://saves is used. Returns the
    /// written path, or null on failure.
    /// </summary>
    public static string? Save(Node systemContainer, string? path = null)
    {
        if (systemContainer == null)
        {
            GameLogger.Error("[SaveManager] Save called with null system_container.");
            return null;
        }

        var dto = new SaveFileDto();

        // --- Meta ---
        dto.Meta = new MetaDto
        {
            SaveVersion = SaveYaml.CurrentVersion,
            GameVersion = ProjectSettings.GetSetting("application/config/version", "").AsString(),
            CreatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
        };

        // --- Session + barycenter ---
        var systemData = systemContainer.GetNodeOrNull<SystemData>("SystemData");
        if (systemData != null)
        {
            dto.Session.SystemName = systemData.SystemName;
            dto.Session.CompanyName = systemData.CompanyName;
            dto.Session.IsGameStarted = systemData.IsGameStarted;
            dto.Meta.TemplateName = systemData.SystemName;
        }

        var barycenter = FindChild<Barycenter>(systemContainer);
        if (barycenter != null)
        {
            dto.Session.Barycenter = new BarycenterDto
            {
                SystemName = barycenter.SystemName,
                SectorId = barycenter.SectorId,
                Position = Vec3Dto.From(barycenter.Position),
                Velocity = Vec3Dto.From(barycenter.Velocity),
                Mass = barycenter.Mass,
            };
        }

        // --- Serializable singletons (trackers) ---
        // Discovered via the "save_serializable" group; each owns its Serialize() and is routed into
        // the typed SaveFileDto field by SaveKey. Adding a tracker needs no edit here beyond a new case.
        foreach (var node in systemContainer.GetTree().GetNodesInGroup("save_serializable"))
        {
            if (node is not ISaveSerializable savable)
                continue;
            var section = savable.Serialize();
            switch (savable.SaveKey)
            {
                case "company" when section is CompanyDto company:
                    dto.Company = company;
                    break;
                case "economy" when section is EconomyDto economy:
                    dto.Economy = economy;
                    break;
                case "time" when section is TimeDto time:
                    dto.Time = time;
                    break;
                default:
                    GameLogger.Warning(
                        $"[SaveManager] Unhandled serializable section '{savable.SaveKey}' ({section?.GetType().Name}).");
                    break;
            }
        }

        // --- Bodies (celestial first, then their satellite descendants) ---
        foreach (var child in systemContainer.GetChildren())
        {
            if (child is not CelestialBody body)
                continue;

            dto.Bodies.Add((BodyDto)body.Serialize());

            foreach (var sat in FindSatellites(body))
                dto.Bodies.Add((BodyDto)sat.Serialize());
        }

        // --- Logistics units (registry covers orbiting + adrift-in-transit ships uniformly) ---
        var units = systemData?.GetAllShips();
        if (units != null && units.Count > 0)
        {
            dto.LogisticsUnits = new System.Collections.Generic.List<LogisticsUnitDto>(units.Count);
            foreach (var unit in units)
                dto.LogisticsUnits.Add((LogisticsUnitDto)unit.Serialize());
        }

        // --- Buildings + stations (Phase 3, save_version 3) ---
        var buildings = new System.Collections.Generic.List<BuildingDto>();
        var stations = new System.Collections.Generic.List<StationDto>();
        foreach (var child in systemContainer.GetChildren())
        {
            if (child is not CelestialBody body)
                continue;
            CollectStructures(body, buildings, stations);
            foreach (var sat in FindSatellites(body))
                CollectStructures(sat, buildings, stations);
        }
        if (buildings.Count > 0)
            dto.Buildings = buildings;
        if (stations.Count > 0)
            dto.Stations = stations;

        path ??= DefaultSavePath(dto.Session.SystemName);
        if (!SaveYaml.WriteToFile(path, dto))
            return null;

        GameLogger.Info($"[SaveManager] Saved {dto.Bodies.Count} bodies to '{path}'.");
        return path;
    }

    public static string DefaultSavePath(string systemName)
    {
        string slug = Sanitize(string.IsNullOrEmpty(systemName) ? "save" : systemName);
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        return $"{SaveYaml.SaveDir}/{slug}-{stamp}.yaml";
    }

    private static string Sanitize(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (char c in s)
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_');
        return sb.ToString();
    }

    private static T? FindChild<T>(Node parent) where T : Node
    {
        foreach (var child in parent.GetChildren())
            if (child is T t)
                return t;
        return null;
    }

    private static System.Collections.Generic.List<CelestialBody> FindSatellites(Node root)
    {
        var result = new System.Collections.Generic.List<CelestialBody>();
        CollectSatellites(root, result);
        return result;
    }

    private static void CollectSatellites(Node node, System.Collections.Generic.List<CelestialBody> acc)
    {
        foreach (var child in node.GetChildren())
        {
            // Satellites are now CelestialBody instances distinguished by classification.
            if (child is CelestialBody cb
                && cb.Classification is Structures.BodyClassification.Satellite
                    or Structures.BodyClassification.Belt)
                acc.Add(cb);
            // Recurse (satellites may sit under a container child of the body).
            CollectSatellites(child, acc);
        }
    }

    /// <summary>
    /// Collects building + station DTOs from a single body. Buildings are discovered through the
    /// body's restored continent/cell graph (deduped by id since a building can span cells); stations
    /// are the StationSatellite children of the body's SatellitesContainer.
    /// </summary>
    private static void CollectStructures(
        IOrbitalBody body,
        System.Collections.Generic.List<BuildingDto> buildings,
        System.Collections.Generic.List<StationDto> stations)
    {
        string bodyName = ((Node)body).Name;

        var continents = body.Mesh?.Continents;
        if (continents != null)
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var continent in continents.Values)
            {
                if (continent?.cells == null)
                    continue;
                foreach (var cell in continent.cells)
                {
                    var b = cell.Building;
                    if (b == null || string.IsNullOrEmpty(b.Id) || !seen.Add(b.Id))
                        continue;
                    buildings.Add(BuildingMapper.ToDto(b, bodyName));
                }
            }
        }

        var container = body.SatellitesContainer;
        if (container != null)
            foreach (var c in container.GetChildren())
                if (c is StationSatellite station)
                    stations.Add((StationDto)station.Serialize());
    }

}
