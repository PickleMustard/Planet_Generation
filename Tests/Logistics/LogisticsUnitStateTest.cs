using GdUnit4;
using Constructables.ArtificialSatellites;
using Structures.Enums;
using static GdUnit4.Assertions;

namespace Tests.Logistics;

/// <summary>
/// Tests for LogisticsUnit state machine transitions, particularly the new
/// Stranded state and its interaction with InTransit and Disabled states.
/// Requires Godot runtime since LogisticsUnit extends Node3D.
/// </summary>
[TestSuite]
public class LogisticsUnitStateTest
{
    /// <summary>
    /// Helper to create a LogisticsUnit and force it into a specific state.
    /// </summary>
    private static LogisticsUnit CreateUnitInState(LogisticsUnitState initialState)
    {
        var unit = new LogisticsUnit();
        unit.State = initialState;
        return unit;
    }

    // ========================================================================
    // STRANDED TRANSITION TESTS
    // ========================================================================

    [TestCase]
    [RequireGodotRuntime]
    public void InTransit_To_Stranded_IsValid()
    {
        var unit = CreateUnitInState(LogisticsUnitState.InTransit);
        AssertThat(unit.CanTransitionTo(LogisticsUnitState.Stranded)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void InTransit_To_Stranded_TransitionSucceeds()
    {
        var unit = CreateUnitInState(LogisticsUnitState.InTransit);
        bool result = unit.TransitionTo(LogisticsUnitState.Stranded);
        AssertThat(result).IsTrue();
        AssertThat(unit.State).IsEqual(LogisticsUnitState.Stranded);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Stranded_To_Idle_IsValid()
    {
        var unit = CreateUnitInState(LogisticsUnitState.Stranded);
        AssertThat(unit.CanTransitionTo(LogisticsUnitState.Idle)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Stranded_To_Idle_TransitionSucceeds()
    {
        var unit = CreateUnitInState(LogisticsUnitState.Stranded);
        bool result = unit.TransitionTo(LogisticsUnitState.Idle);
        AssertThat(result).IsTrue();
        AssertThat(unit.State).IsEqual(LogisticsUnitState.Idle);
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Stranded_To_Disabled_IsValid()
    {
        var unit = CreateUnitInState(LogisticsUnitState.Stranded);
        AssertThat(unit.CanTransitionTo(LogisticsUnitState.Disabled)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Stranded_To_Planning_IsInvalid()
    {
        var unit = CreateUnitInState(LogisticsUnitState.Stranded);
        AssertThat(unit.CanTransitionTo(LogisticsUnitState.Planning)).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Stranded_To_InTransit_IsInvalid()
    {
        var unit = CreateUnitInState(LogisticsUnitState.Stranded);
        AssertThat(unit.CanTransitionTo(LogisticsUnitState.InTransit)).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Stranded_To_Arriving_IsInvalid()
    {
        var unit = CreateUnitInState(LogisticsUnitState.Stranded);
        AssertThat(unit.CanTransitionTo(LogisticsUnitState.Arriving)).IsFalse();
    }

    // ========================================================================
    // EXISTING TRANSITION VALIDATION (ensuring no regressions)
    // ========================================================================

    [TestCase]
    [RequireGodotRuntime]
    public void Idle_To_Planning_IsValid()
    {
        var unit = CreateUnitInState(LogisticsUnitState.Idle);
        AssertThat(unit.CanTransitionTo(LogisticsUnitState.Planning)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Idle_To_Disabled_IsValid()
    {
        var unit = CreateUnitInState(LogisticsUnitState.Idle);
        AssertThat(unit.CanTransitionTo(LogisticsUnitState.Disabled)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void InTransit_To_Arriving_IsValid()
    {
        var unit = CreateUnitInState(LogisticsUnitState.InTransit);
        AssertThat(unit.CanTransitionTo(LogisticsUnitState.Arriving)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void InTransit_To_Disabled_IsValid()
    {
        var unit = CreateUnitInState(LogisticsUnitState.InTransit);
        AssertThat(unit.CanTransitionTo(LogisticsUnitState.Disabled)).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void Disabled_To_Idle_IsValid()
    {
        var unit = CreateUnitInState(LogisticsUnitState.Disabled);
        AssertThat(unit.CanTransitionTo(LogisticsUnitState.Idle)).IsTrue();
    }

    // ========================================================================
    // IsStateValidForOperation TESTS
    // ========================================================================

    [TestCase]
    [RequireGodotRuntime]
    public void IsStateValidForOperation_False_WhenStranded()
    {
        var unit = CreateUnitInState(LogisticsUnitState.Stranded);
        AssertThat(unit.IsStateValidForOperation()).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void IsStateValidForOperation_False_WhenDisabled()
    {
        var unit = CreateUnitInState(LogisticsUnitState.Disabled);
        AssertThat(unit.IsStateValidForOperation()).IsFalse();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void IsStateValidForOperation_True_WhenIdle()
    {
        var unit = CreateUnitInState(LogisticsUnitState.Idle);
        AssertThat(unit.IsStateValidForOperation()).IsTrue();
    }

    [TestCase]
    [RequireGodotRuntime]
    public void IsStateValidForOperation_True_WhenInTransit()
    {
        var unit = CreateUnitInState(LogisticsUnitState.InTransit);
        AssertThat(unit.IsStateValidForOperation()).IsTrue();
    }
}
