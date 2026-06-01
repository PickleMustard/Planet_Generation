using GdUnit4;
using static GdUnit4.Assertions;
using Constructables;
using ProceduralGeneration.PlanetGeneration;
using UtilityLibrary.SaveLoad.Dto;
using UtilityLibrary.SaveLoad.Mappers;

namespace Tests.SaveLoad;

/// <summary>
/// Verifies the LogisticsUnit.Id round-trip path through the save/load layer:
/// ToDto emits the Id, Restore applies it via SetPersistedId BEFORE the unit is
/// added to the scene tree (so _EnterTree's Guid-when-empty branch is skipped).
/// </summary>
[TestSuite]
public class LogisticsIdRoundTripTest
{
    private static IOrbitalBody? NoBody(string _) => null;

    [TestCase]
    [RequireGodotRuntime]
    public void ToDto_IncludesId()
    {
        var unit = new LogisticsUnit { Name = "Source" };
        unit.SetPersistedId("source-id");

        var dto = LogisticsMapper.ToDto(unit);

        AssertThat(dto.Id).IsEqual("source-id");

        unit.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Restore_AppliesPersistedIdBeforeTreeAdd()
    {
        var dto = new LogisticsUnitDto
        {
            Name = "Restored",
            Id = "restored-id",
            State = "Idle",
        };

        var unit = LogisticsMapper.Restore(dto, NoBody);

        // Restore returns a unit not yet parented; _EnterTree has not fired. The Id
        // must already be the persisted one — that's the whole point of SetPersistedId.
        AssertThat(unit.Id).IsEqual("restored-id");
        AssertThat(unit.IsInsideTree()).IsFalse();

        unit.Free();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Restore_EmptyId_LeavesIdEmptyUntilEnterTree()
    {
        var dto = new LogisticsUnitDto
        {
            Name = "Legacy",
            Id = "", // pre-Id save
            State = "Idle",
        };

        var unit = LogisticsMapper.Restore(dto, NoBody);

        // Pre-tree-add: Id is still empty so the _EnterTree Guid branch can fire on AddChild.
        AssertThat(unit.Id).IsEqual("");

        unit.Free();
    }
}
