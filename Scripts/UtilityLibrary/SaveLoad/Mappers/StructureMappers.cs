using System;
using System.Collections.Generic;
using System.Linq;
using Constructables;
using Constructables.Buildings.Behaviors;
using Constructables.Stations.Behaviors;
using Godot;
using Logistics.Resources;
using Structures.Enums;
using Structures.GameState;
using Structures.Logistics;
using Structures.Resources;
using Structures.Transfers;
using UtilityLibrary;
using UtilityLibrary.SaveLoad.Dto;

namespace UtilityLibrary.SaveLoad.Mappers;

/// <summary>
/// Maps placed <see cref="Building"/> instances to <see cref="BuildingDto"/> and back (save_version 3).
/// Definition is referenced by id and re-instantiated via <see cref="BuildingDatabase"/>; only mutable
/// state (placement, storage contents, recipe, transfer orders/schedules) is serialized. Manufacturing
/// cycle progress is discarded — a restored building restarts its recipe fresh.
/// </summary>
public static class BuildingMapper
{
    // ---------- Save ----------

    public static BuildingDto ToDto(Building b, string bodyName)
    {
        var dto = new BuildingDto
        {
            Id = b.Id,
            DefinitionId = b.Definition?.IdName ?? "",
            BodyName = bodyName,
            PrimaryCellIndex = b.PrimaryCell?.Index ?? -1,
            PoweredOn = b.PoweredOn,
            Specifier = b.Specifier,
            SpeedModifier = b.SpeedModifier,
            ActiveRecipeId = b.ActiveRecipeId,
        };

        foreach (var cell in b.OccupiedCells)
            if (b.PrimaryCell == null || cell.Index != b.PrimaryCell.Index)
                dto.AdditionalCellIndices.Add(cell.Index);

        CopyStorage(b.InputStorage, dto.InputStorage);
        CopyStorage(b.OutputStorage, dto.OutputStorage);
        CopyStorage(b.BulkStorage, dto.BulkStorage);

        var transfer = b.GetBehavior<TransferStationBehavior>();
        if (transfer != null)
            dto.Transfer = TransferMapper.ToDto(
                transfer.TotalTime,
                transfer.GetActiveTransfers().Select(t => t.Order),
                transfer.GetAllSchedules());

        var ext = b.GetBehavior<ExtractionBehavior>();
        if (ext != null)
        {
            dto.ExtractionSlots = new List<ExtractionSlotDto>();
            foreach (var slot in ext.Slots)
                dto.ExtractionSlots.Add(new ExtractionSlotDto
                {
                    Kind = slot.Kind.ToString(),
                    ResourceId = slot.ResourceId,
                });
        }

        return dto;
    }

    // ---------- Load ----------

    public static Building? Restore(
        BuildingDto dto,
        IOrbitalBody body,
        IReadOnlyDictionary<int, VoronoiCell> cellIndex)
    {
        if (BuildingDatabase.Instance == null
            || !BuildingDatabase.Instance.TryGetBuilding(dto.DefinitionId, out var def)
            || def == null)
        {
            GameLogger.Warning(
                $"[BuildingMapper] Building definition '{dto.DefinitionId}' not found; skipping '{dto.Id}'.");
            return null;
        }

        if (!cellIndex.TryGetValue(dto.PrimaryCellIndex, out var primary))
        {
            GameLogger.Warning(
                $"[BuildingMapper] Primary cell {dto.PrimaryCellIndex} not found on '{dto.BodyName}'; skipping building '{dto.Id}'.");
            return null;
        }

        var building = def.Instantiate();
        building.Id = dto.Id;
        building.PoweredOn = dto.PoweredOn;
        building.Specifier = dto.Specifier;
        building.SpeedModifier = dto.SpeedModifier;

        List<VoronoiCell>? additional = null;
        if (dto.AdditionalCellIndices.Count > 0)
        {
            additional = new List<VoronoiCell>(dto.AdditionalCellIndices.Count);
            foreach (var idx in dto.AdditionalCellIndices)
                if (cellIndex.TryGetValue(idx, out var c))
                    additional.Add(c);
        }
        building.SetPlacement(primary, additional);

        var parentNode = (Node3D)body;
        var visual = new BuildingNode { Name = def.DisplayName ?? def.IdName ?? "Building" };
        parentNode.AddChild(visual);
        visual.Bind(building, parentNode, body.Radius);

        foreach (var cell in building.OccupiedCells)
            cell.Building = building;

        // Set the recipe before Register so recipe-aware behaviors (ExtractionBehavior's
        // deposit/slot resolution) build against the saved recipe rather than the default.
        building.ActiveRecipeId = dto.ActiveRecipeId;
        building.Register();

        building.InputStorage.RestoreContents(dto.InputStorage);
        building.OutputStorage.RestoreContents(dto.OutputStorage);
        building.BulkStorage.RestoreContents(dto.BulkStorage);

        if (dto.ExtractionSlots != null)
        {
            var ext = building.GetBehavior<ExtractionBehavior>();
            if (ext != null)
            {
                var slots = new List<(ExtractionSlotKind, string?)>(dto.ExtractionSlots.Count);
                foreach (var s in dto.ExtractionSlots)
                {
                    var kind = s.Kind == nameof(ExtractionSlotKind.Secondary)
                        ? ExtractionSlotKind.Secondary
                        : ExtractionSlotKind.Primary;
                    slots.Add((kind, s.ResourceId));
                }
                ext.RestoreSlots(slots);
            }
        }

        if (dto.Transfer != null)
        {
            var beh = building.GetBehavior<TransferStationBehavior>();
            beh?.RestoreState(
                dto.Transfer.TotalTime,
                TransferMapper.RestoreOrders(dto.Transfer.ActiveTransfers),
                TransferMapper.RestoreSchedules(dto.Transfer.Schedules));
        }

        return building;
    }

    /// <summary>Builds a cell-index → cell lookup from a body's restored continent graph.</summary>
    public static Dictionary<int, VoronoiCell> BuildCellIndex(IOrbitalBody body)
    {
        var map = new Dictionary<int, VoronoiCell>();
        var continents = body.Mesh?.Continents;
        if (continents == null)
            return map;
        foreach (var continent in continents.Values)
        {
            if (continent?.cells == null)
                continue;
            foreach (var cell in continent.cells)
                map[cell.Index] = cell;
        }
        return map;
    }

    private static void CopyStorage(Storage storage, Dictionary<string, int> into)
    {
        foreach (var kv in storage.GetAllQuantities())
            into[kv.Key] = kv.Value;
    }
}

/// <summary>
/// Load half for orbital <see cref="StationSatellite"/> instances. The station is rebuilt from its
/// <see cref="StationDefinition"/> (via <see cref="StationDatabase"/>), re-attached under the host body's
/// SatellitesContainer with its saved id and orbital arc, then brought online via
/// <see cref="StationSatellite.ActivateFromSave"/>. The save half lives on the station itself
/// (<c>StationSatellite.Serialize()</c>).
/// </summary>
public static class StationMapper
{
    public static StationSatellite? Restore(StationDto dto, IOrbitalBody body)
    {
        if (StationDatabase.Instance == null
            || !StationDatabase.Instance.TryGetStation(dto.DefinitionName, out var def)
            || def == null)
        {
            GameLogger.Warning(
                $"[StationMapper] Station definition '{dto.DefinitionName}' not found; skipping '{dto.Id}'.");
            return null;
        }

        var station = new StationSatellite { Name = string.IsNullOrEmpty(dto.Name) ? dto.Id : dto.Name };
        body.SatellitesContainer.AddChild(station); // _EnterTree assigns a fresh GUID here.
        station.SetSavedId(dto.Id);                  // Override with the saved id (endpoints key off it).
        station.ParentBody = body;
        station.IsStationary = dto.IsStationary;

        station.SetStationDefinition(def);
        station.RestoreOrbitalState(
            body,
            dto.BandIndex,
            dto.OrbitalAngle,
            dto.OrbitalRadius,
            dto.OrbitalSpeed,
            dto.HostMass,
            dto.Velocity.ToVector3());
        station.Position = dto.Position.ToVector3();

        station.ActivateFromSave();
        station.IsActive = dto.IsActive;

        station.BulkStorage.RestoreContents(dto.BulkStorage);

        if (dto.Transfer != null)
        {
            var hub = station.GetBehavior<TransferHubBehavior>();
            hub?.RestoreState(
                dto.Transfer.TotalTime,
                TransferMapper.RestoreOrders(dto.Transfer.ActiveTransfers),
                TransferMapper.RestoreSchedules(dto.Transfer.Schedules));
        }

        if (dto.MarketState != null)
        {
            var market = station.GetBehavior<MarketStationBehavior>();
            market?.RestoreState(dto.MarketState);
        }

        return station;
    }
}

/// <summary>
/// Maps transfer orders/schedules (and their destinations + manifests) to DTOs and back. Used by both
/// <see cref="BuildingMapper"/> and <see cref="StationMapper"/> since surface stations and orbital hubs
/// share the same transfer domain types.
/// </summary>
public static class TransferMapper
{
    public static TransferBehaviorStateDto ToDto(
        double totalTime,
        IEnumerable<TransferOrder> orders,
        IEnumerable<TransferSchedule> schedules)
    {
        var dto = new TransferBehaviorStateDto { TotalTime = totalTime };
        foreach (var o in orders)
            dto.ActiveTransfers.Add(OrderToDto(o));
        foreach (var s in schedules)
            dto.Schedules.Add(ScheduleToDto(s));
        return dto;
    }

    public static List<TransferOrder> RestoreOrders(List<TransferOrderDto> dtos) =>
        dtos.Select(OrderFromDto).ToList();

    public static List<TransferSchedule> RestoreSchedules(List<TransferScheduleDto> dtos) =>
        dtos.Select(ScheduleFromDto).ToList();

    private static TransferOrderDto OrderToDto(TransferOrder o)
    {
        var dto = new TransferOrderDto
        {
            OrderId = o.OrderId,
            OriginBuildingId = o.OriginBuildingId,
            Destination = DestToDto(o.Destination),
            State = o.State.ToString(),
            TravelTimeSeconds = o.TravelTimeSeconds,
            ElapsedTimeSeconds = o.ElapsedTimeSeconds,
            DispatchedAtTime = o.DispatchedAtTime,
            SourceScheduleId = o.SourceScheduleId,
        };
        foreach (var kv in o.Manifest.Resources)
            dto.Manifest[kv.Key] = kv.Value;
        foreach (var kv in o.RequestedManifest.Resources)
            dto.RequestedManifest[kv.Key] = kv.Value;
        return dto;
    }

    private static TransferOrder OrderFromDto(TransferOrderDto dto)
    {
        var order = new TransferOrder
        {
            OrderId = dto.OrderId,
            OriginBuildingId = dto.OriginBuildingId,
            Destination = DestFromDto(dto.Destination),
            State = ParseEnum(dto.State, SurfaceTransferState.InTransit),
            TravelTimeSeconds = dto.TravelTimeSeconds,
            ElapsedTimeSeconds = dto.ElapsedTimeSeconds,
            DispatchedAtTime = dto.DispatchedAtTime,
            SourceScheduleId = dto.SourceScheduleId,
        };
        foreach (var kv in dto.Manifest)
            order.Manifest.LoadResource(kv.Key, kv.Value);
        foreach (var kv in dto.RequestedManifest)
            order.RequestedManifest.LoadResource(kv.Key, kv.Value);
        return order;
    }

    private static TransferScheduleDto ScheduleToDto(TransferSchedule s)
    {
        var dto = new TransferScheduleDto
        {
            ScheduleId = s.ScheduleId,
            OriginBuildingId = s.OriginBuildingId,
            Destination = DestToDto(s.Destination),
            DepartureMode = s.DepartureMode.ToString(),
            Threshold = s.Threshold.ToString(),
            State = s.State.ToString(),
            ActiveTransferOrderId = s.ActiveTransferOrderId,
            Priority = s.Priority,
            WaitSeconds = s.WaitSeconds,
            LastDispatchTime = s.LastDispatchTime,
        };
        foreach (var kv in s.ResourceProportions)
            dto.ResourceProportions[kv.Key] = kv.Value;
        return dto;
    }

    private static TransferSchedule ScheduleFromDto(TransferScheduleDto dto)
    {
        return new TransferSchedule
        {
            ScheduleId = dto.ScheduleId,
            OriginBuildingId = dto.OriginBuildingId,
            Destination = DestFromDto(dto.Destination),
            ResourceProportions = new Dictionary<string, float>(dto.ResourceProportions),
            DepartureMode = ParseEnum(dto.DepartureMode, DepartureConditionMode.AllResources),
            Threshold = ParseEnum(dto.Threshold, DepartureThreshold.Full),
            State = ParseEnum(dto.State, TransferScheduleState.Idle),
            ActiveTransferOrderId = dto.ActiveTransferOrderId,
            Priority = dto.Priority,
            WaitSeconds = dto.WaitSeconds,
            LastDispatchTime = dto.LastDispatchTime,
        };
    }

    private static TransferDestinationDto DestToDto(TransferDestination d) => new()
    {
        BuildingId = d.BuildingId,
        StationSatelliteId = d.StationSatelliteId,
    };

    private static TransferDestination DestFromDto(TransferDestinationDto d) => new()
    {
        BuildingId = d.BuildingId,
        StationSatelliteId = d.StationSatelliteId,
    };

    private static T ParseEnum<T>(string s, T fallback) where T : struct =>
        Enum.TryParse<T>(s, out var v) ? v : fallback;
}
