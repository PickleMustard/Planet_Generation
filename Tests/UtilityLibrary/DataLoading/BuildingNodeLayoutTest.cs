using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using System.Collections.Generic;
using Structures.Enums;
using Structures.Resources;

namespace Tests.UtilityLibrary.DataLoading;

/// <summary>
/// Verifies that <see cref="BuildingDefinition.PopulateNodeLayoutFromShape"/>
/// builds the runtime node layout from the referenced shape's per-side slot
/// declarations. Shapes are now the source of truth for node layout; building
/// YAMLs carry only <c>shape_id</c>.
/// </summary>
[TestSuite]
public class BuildingNodeLayoutTest
{
    [TestCase]
    public void PopulateFromShape_BuildsLayoutFromSlots()
    {
        var shape = new BuildingShape2D
        {
            Id = "test_shape",
            Vertices = new[]
            {
                new Vector2(0, -1), new Vector2(1, 0),
                new Vector2(0, 1), new Vector2(-1, 0),
            },
            Sides = new List<BuildingShape2D.SideSpec>
            {
                new() { Slots =
                    {
                        new BuildingShape2D.SlotSpec { Kind = ResourceNodeKind.Import, StateOfMatter = StateOfMatter.Solid },
                        new BuildingShape2D.SlotSpec { Kind = ResourceNodeKind.Import, StateOfMatter = StateOfMatter.Fluid },
                    } },
                new() { Slots = { new BuildingShape2D.SlotSpec { Kind = ResourceNodeKind.Export, StateOfMatter = StateOfMatter.Solid } } },
                new() { Slots = { new BuildingShape2D.SlotSpec { Kind = ResourceNodeKind.Flex, StateOfMatter = StateOfMatter.Fluid } } },
                new() { },
            },
        };

        var def = new BuildingDefinition { IdName = "t" };
        def.PopulateNodeLayoutFromShape(shape);

        AssertThat(def.NodeLayout.Count).IsEqual(4);
        AssertThat(def.NodeLayout[0].SideIndex).IsEqual(0);
        AssertThat(def.NodeLayout[0].SlotIndex).IsEqual(0);
        AssertThat(def.NodeLayout[0].Kind).IsEqual(ResourceNodeKind.Import);
        AssertThat(def.NodeLayout[0].StateOfMatter).IsEqual(StateOfMatter.Solid);
        AssertThat(def.NodeLayout[1].SideIndex).IsEqual(0);
        AssertThat(def.NodeLayout[1].SlotIndex).IsEqual(1);
        AssertThat(def.NodeLayout[1].Kind).IsEqual(ResourceNodeKind.Import);
        AssertThat(def.NodeLayout[1].StateOfMatter).IsEqual(StateOfMatter.Fluid);
        AssertThat(def.NodeLayout[2].SideIndex).IsEqual(1);
        AssertThat(def.NodeLayout[2].Kind).IsEqual(ResourceNodeKind.Export);
        AssertThat(def.NodeLayout[2].StateOfMatter).IsEqual(StateOfMatter.Solid);
        AssertThat(def.NodeLayout[3].SideIndex).IsEqual(2);
        AssertThat(def.NodeLayout[3].Kind).IsEqual(ResourceNodeKind.Flex);
        AssertThat(def.NodeLayout[3].StateOfMatter).IsEqual(StateOfMatter.Fluid);
    }

    [TestCase]
    public void PopulateFromShape_EmptySidesYieldsNoNodes()
    {
        var shape = new BuildingShape2D
        {
            Id = "empty",
            Vertices = new[]
            {
                new Vector2(0, -1), new Vector2(1, 0),
                new Vector2(0, 1), new Vector2(-1, 0),
            },
            Sides = new List<BuildingShape2D.SideSpec>
            {
                new(), new(), new(), new(),
            },
        };

        var def = new BuildingDefinition { IdName = "empty" };
        def.PopulateNodeLayoutFromShape(shape);
        AssertThat(def.NodeLayout.Count).IsEqual(0);
    }

    [TestCase]
    public void PopulateFromShape_Replaces_ExistingLayout()
    {
        var def = new BuildingDefinition { IdName = "repop" };
        def.NodeLayout.Add(new NodeSpec { SideIndex = 9, Kind = ResourceNodeKind.Flex });

        var shape = new BuildingShape2D
        {
            Id = "small",
            Vertices = new[]
            {
                new Vector2(0, -1), new Vector2(0.866f, 0.5f), new Vector2(-0.866f, 0.5f),
            },
            Sides = new List<BuildingShape2D.SideSpec>
            {
                new() { Slots = { new BuildingShape2D.SlotSpec { Kind = ResourceNodeKind.Export, StateOfMatter = StateOfMatter.Solid } } },
                new(),
                new(),
            },
        };
        def.PopulateNodeLayoutFromShape(shape);

        AssertThat(def.NodeLayout.Count).IsEqual(1);
        AssertThat(def.NodeLayout[0].Kind).IsEqual(ResourceNodeKind.Export);
        AssertThat(def.NodeLayout[0].StateOfMatter).IsEqual(StateOfMatter.Solid);
    }
}
