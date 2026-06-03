using System.Collections.Generic;
using Godot;
using ProceduralGeneration;
using ProceduralGeneration.PlanetGeneration;
using Structures.GameState;
using UtilityLibrary.SaveLoad.Dto;
using UtilityLibrary.SaveLoad.Mappers;
using UtilityLibrary.SaveLoad.Migrations;

namespace UtilityLibrary.SaveLoad;

/// <summary>
/// Drives the load path that bypasses SystemGenerator: instead of generating a system it rebuilds
/// the saved bodies (with restored geometry) and re-derives their renderable meshes, then restores
/// the company/economy/session trackers once the GameScene exists.
///
/// Flow: <see cref="BeginLoad"/> (read+migrate) → <see cref="BuildSystem"/> (build bodies into a
/// container, in the LoadingScreen tree) → scene swap reparents the bodies into GameScene's
/// system_container → GameScene._Ready calls <see cref="RestoreSession"/>.
/// </summary>
public static class SaveLoader
{
    /// <summary>The save currently being loaded. Non-null between BeginLoad and RestoreSession.</summary>
    public static SaveFileDto? PendingLoad { get; private set; }

    /// <summary>
    /// Restored buildings/stations awaiting registration with the ManufactureTickEngine. Populated in
    /// <see cref="BuildSystem"/> (engine not yet alive) and drained in <see cref="RestoreSession"/>
    /// once SystemData._Ready has started the engine.
    /// </summary>
    private static readonly List<Constructables.Tick.IManufactureTickable> _pendingTickables = new();

    public static bool IsLoadPending => PendingLoad != null;

    /// <summary>Reads and migrates a save file, staging it for the build/restore steps.</summary>
    public static bool BeginLoad(string path)
    {
        var dto = SaveYaml.ReadFromFile(path);
        if (dto == null)
            return false;

        if (!SaveMigrator.TryMigrate(dto, out var error))
        {
            GameLogger.Error($"[SaveLoader] Cannot load '{path}': {error}");
            return false;
        }

        PendingLoad = dto;
        return true;
    }

    /// <summary>
    /// Builds all bodies from the pending save into <paramref name="container"/> (which must already
    /// be in the scene tree so GlobalPosition / _Ready behave), wires orbital parents, runs orbit
    /// initialization, and adds the N-body coordinator. Does NOT restore trackers — see
    /// <see cref="RestoreSession"/>.
    /// </summary>
    public static void BuildSystem(Node container)
    {
        if (PendingLoad == null)
        {
            GameLogger.Error("[SaveLoader] BuildSystem called with no pending load.");
            return;
        }

        // Freeze per-body physics until the coordinator takes over, matching generation semantics.
        CelestialBody.CoordinatorActive = true;

        var save = PendingLoad;

        // Barycenter node + static reference, mirroring SystemGenerator.
        var bc = save.Session.Barycenter;
        var barycenter = new Barycenter(bc.Position.ToVector3(), bc.Velocity.ToVector3(), bc.Mass)
        {
            SystemName = bc.SystemName,
            SectorId = bc.SectorId,
        };
        CelestialBody.barycenter = barycenter;
        container.AddChild(barycenter);

        var celestialByName = new Dictionary<string, CelestialBody>();
        var bodyNodes = new List<(Node3D Node, BodyDto Dto)>();

        // Pass 1: celestial bodies.
        foreach (var dto in save.Bodies)
        {
            if (dto.Kind == "Satellite")
                continue;
            var node = (CelestialBody)BodyMapper.Restore(dto);
            container.AddChild(node);
            node.Position = dto.Position.ToVector3();
            celestialByName[dto.Name] = node;
            bodyNodes.Add((node, dto));
        }

        // Pass 2: satellites parented to their celestial host node.
        foreach (var dto in save.Bodies)
        {
            if (dto.Kind != "Satellite")
                continue;
            var node = (CelestialBody)BodyMapper.Restore(dto);

            CelestialBody? parent = null;
            if (!string.IsNullOrEmpty(dto.OrbitalParentName))
                celestialByName.TryGetValue(dto.OrbitalParentName!, out parent);

            if (parent != null)
            {
                parent.AddChild(node);
                node.OrbitalParent = parent;
            }
            else
            {
                GameLogger.Warning(
                    $"[SaveLoader] Satellite '{dto.Name}' has no resolvable parent '{dto.OrbitalParentName}'; parenting to container.");
                container.AddChild(node);
            }
            node.Position = dto.Position.ToVector3();
            bodyNodes.Add((node, dto));
        }

        // Pass 3: resolve celestial OrbitalParent links by name.
        foreach (var dto in save.Bodies)
        {
            if (dto.Kind == "Satellite" || string.IsNullOrEmpty(dto.OrbitalParentName))
                continue;
            if (celestialByName.TryGetValue(dto.Name, out var node)
                && celestialByName.TryGetValue(dto.OrbitalParentName!, out var parent))
            {
                node.OrbitalParent = parent;
            }
        }

        // Pass 4: orbit system init + band-count restore.
        foreach (var (node, dto) in bodyNodes)
        {
            if (node is CelestialBody cb)
                cb.InitializeOrbitSystem();
            BodyMapper.ApplyPostInit(node, dto);
        }

        // Pass 5: logistics units (save_version ≥ 2). Each orbits a host body (under its
        // SatellitesContainer) or is adrift in the container when in transit / stranded.
        var restoredUnits = new List<(LogisticsUnitDto Dto, Constructables.LogisticsUnit Unit)>();
        if (save.LogisticsUnits != null)
        {
            IOrbitalBody? ResolveBody(string name) =>
                celestialByName.TryGetValue(name, out var b) ? b : null;

            foreach (var unitDto in save.LogisticsUnits)
            {
                var unit = LogisticsMapper.Restore(unitDto, ResolveBody);
                restoredUnits.Add((unitDto, unit));

                Node parent = container;
                if (!string.IsNullOrEmpty(unitDto.HostBodyName)
                    && celestialByName.TryGetValue(unitDto.HostBodyName!, out var host)
                    && host.SatellitesContainer != null)
                {
                    parent = host.SatellitesContainer;
                }
                else if (!string.IsNullOrEmpty(unitDto.HostBodyName))
                {
                    GameLogger.Warning(
                        $"[SaveLoader] Logistics unit '{unitDto.Name}' host '{unitDto.HostBodyName}' "
                            + "unresolved; placing adrift in container.");
                }

                parent.AddChild(unit);
                unit.Position = unitDto.Position.ToVector3();
            }

            GameLogger.Info($"[SaveLoader] Built {save.LogisticsUnits.Count} logistics units from save.");
        }

        // Pass 6: buildings + stations (save_version ≥ 3). Restored onto their owning body's
        // restored geometry/orbit; tick-engine registration is deferred to RestoreSession.
        _pendingTickables.Clear();
        if (save.Buildings != null || save.Stations != null)
        {
            var allBodiesByName = new Dictionary<string, IOrbitalBody>();
            foreach (var (node, d) in bodyNodes)
                if (node is IOrbitalBody ob)
                    allBodiesByName[d.Name] = ob;

            if (save.Buildings != null)
            {
                var cellMaps = new Dictionary<string, IReadOnlyDictionary<int, Structures.GameState.VoronoiCell>>();
                foreach (var bdto in save.Buildings)
                {
                    if (!allBodiesByName.TryGetValue(bdto.BodyName, out var body))
                    {
                        GameLogger.Warning(
                            $"[SaveLoader] Building '{bdto.Id}' references unknown body '{bdto.BodyName}'; skipping.");
                        continue;
                    }
                    if (!cellMaps.TryGetValue(bdto.BodyName, out var map))
                    {
                        map = BuildingMapper.BuildCellIndex(body);
                        cellMaps[bdto.BodyName] = map;
                    }
                    var building = BuildingMapper.Restore(bdto, body, map);
                    if (building != null)
                        _pendingTickables.Add(building);
                }
            }

            if (save.Stations != null)
            {
                foreach (var sdto in save.Stations)
                {
                    if (!allBodiesByName.TryGetValue(sdto.BodyName, out var body))
                    {
                        GameLogger.Warning(
                            $"[SaveLoader] Station '{sdto.Id}' references unknown body '{sdto.BodyName}'; skipping.");
                        continue;
                    }
                    var station = StationMapper.Restore(sdto, body);
                    if (station != null)
                        _pendingTickables.Add(station);
                }
            }

            GameLogger.Info(
                $"[SaveLoader] Restored {_pendingTickables.Count} structures (buildings + stations) from save.");
        }

        // Pass 7: orbital schedules (save_version ≥ 4). Restored last because leg endpoints
        // reference stations (pass 6) and bodies by name/id; assigned to each unit's executor
        // via the deferred pending-restore hook.
        if (restoredUnits.Count > 0)
        {
            var bodiesByName = new Dictionary<string, IOrbitalBody>();
            foreach (var (node, d) in bodyNodes)
                if (node is IOrbitalBody ob)
                    bodiesByName[d.Name] = ob;

            var stationsById = new Dictionary<string, Constructables.StationSatellite>();
            foreach (var ob in bodiesByName.Values)
            {
                var satContainer = ob.SatellitesContainer;
                if (satContainer == null) continue;
                foreach (var child in satContainer.GetChildren())
                    if (child is Constructables.StationSatellite station && !string.IsNullOrEmpty(station.Id))
                        stationsById[station.Id] = station;
            }

            IOrbitalBody? ResolveScheduleBody(string name) =>
                bodiesByName.TryGetValue(name, out var b) ? b : null;
            Constructables.StationSatellite? ResolveStation(string id) =>
                stationsById.TryGetValue(id, out var s) ? s : null;

            int restored = 0;
            foreach (var (unitDto, unit) in restoredUnits)
            {
                if (unitDto.Schedule == null) continue;
                var schedule = LogisticsMapper.RestoreSchedule(
                    unitDto.Schedule, ResolveScheduleBody, ResolveStation);
                if (schedule != null)
                {
                    unit.SetPendingScheduleRestore(schedule);
                    restored++;
                }
            }

            if (restored > 0)
                GameLogger.Info($"[SaveLoader] Restored {restored} orbital schedule(s) from save.");
        }

        // Coordinator drives synchronized N-body integration (and re-asserts CoordinatorActive).
        var coordinator = new NBodyCoordinator();
        container.AddChild(coordinator);

        GameLogger.Info($"[SaveLoader] Built {bodyNodes.Count} bodies from save.");
    }

    /// <summary>
    /// Restores company, economy and session state into the trackers under <paramref name="systemContainer"/>.
    /// Must run AFTER the trackers' _Ready (so CompanyDataTracker.Instance / EconomyTracker.Instance and
    /// the seeded market exist). Clears the pending load when done.
    /// </summary>
    public static void RestoreSession(Node systemContainer)
    {
        if (PendingLoad == null)
            return;

        var save = PendingLoad;

        var systemData = systemContainer.GetNodeOrNull<SystemData>("SystemData");
        if (systemData != null)
        {
            systemData.SystemName = save.Session.SystemName;
            systemData.CompanyName = save.Session.CompanyName;
            systemData.IsGameStarted = save.Session.IsGameStarted;
        }

        if (CompanyDataTracker.Instance != null)
            CompanyMapper.Restore(CompanyDataTracker.Instance, save.Company);

        if (EconomyTracker.Instance != null)
            EconomyMapper.Restore(EconomyTracker.Instance, save.Economy);

        // Register restored buildings/stations with the now-running ManufactureTickEngine. Storage
        // capacity was reconstructed directly into slots during restore, so there is no economy
        // RegisterBuilding path to bypass — no double-counting occurs.
        if (_pendingTickables.Count > 0)
        {
            systemData?.RegisterLoadedTickables(_pendingTickables);
            GameLogger.Info($"[SaveLoader] Registered {_pendingTickables.Count} loaded tickables.");
        }
        _pendingTickables.Clear();

        GameLogger.Info("[SaveLoader] Session/company/economy restored.");
        PendingLoad = null;
    }
}
