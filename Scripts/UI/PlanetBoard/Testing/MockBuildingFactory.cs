using System;
using System.Collections.Generic;
using Constructables;
using Constructables.Buildings;
using Godot;
using Structures.Resources;
using Structures.Transfers;

namespace UI.PlanetBoard.Testing;

public static class MockBuildingFactory
{
    public static Building Create(
        string shapeId,
        Color color,
        int importPorts,
        int exportPorts,
        bool isTransferStation = false)
    {
        var def = new BuildingDefinition
        {
            IdName = $"mock_{Guid.NewGuid():N}",
            DisplayName = $"Mock {shapeId}",
            Visual = new VisualDefinition
            {
                ShapeId = shapeId,
                ShapeSize = 64f,
                ShapeColor = color,
            },
        };

        if (isTransferStation)
        {
            var tsConfig = new Dictionary<string, object>
            {
                ["cargo_capacity"] = 500f,
                ["vehicle_speed"] = 50f,
                ["max_concurrent_transfers"] = 2,
            };
            def.BehaviorEntries.Add(new BuildingDefinition.BehaviorConfigEntry
            {
                BehaviorId = "TransferStationBehavior",
                Config = tsConfig,
            });
        }

        var b = new Building();
        b.Id = Guid.NewGuid().ToString();
        b.ApplyDefinition(def);
        b.Name = def.DisplayName!;

        int sideCount = ResolveSideCount(shapeId);
        AddPorts(b, importPorts, ResourceNodeKind.Import, 0, sideCount);
        AddPorts(b, exportPorts, ResourceNodeKind.Export, importPorts, sideCount);
        return b;
    }

    private static int ResolveSideCount(string shapeId)
    {
        var shape = BuildingShape2DDatabase.Instance.Get(shapeId);
        return shape?.SideCount > 0 ? shape.SideCount : 6;
    }

    private static void AddPorts(Building b, int count, ResourceNodeKind kind, int offset, int sideCount)
    {
        for (int i = 0; i < count; i++)
        {
            b.Nodes.Add(new ResourceNode
            {
                Owner = b,
                Kind = kind,
                SideIndex = (offset + i) % sideCount,
                SlotIndex = 0,
            });
        }
    }
}
