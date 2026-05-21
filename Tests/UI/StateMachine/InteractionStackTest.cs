using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using UI.StateMachine;

namespace Tests.UI.StateMachine;

[TestSuite]
[RequireGodotRuntime]
public class InteractionStackTest
{
	private Blackboard _bb = null!;

	[BeforeTest]
	public void Setup()
	{
		_bb = new Blackboard();
	}

	// ───────── Push / Pop / Peek basics ─────────

	[TestCase]
	public void Push_IncreasesDepth()
	{
		AssertThat(InteractionStack.Depth(_bb)).IsEqual(0);

		InteractionStack.Push(_bb, "window_closed", new Godot.Collections.Dictionary());
		AssertThat(InteractionStack.Depth(_bb)).IsEqual(1);

		InteractionStack.Push(_bb, "back_to_orbital_body", new Godot.Collections.Dictionary());
		AssertThat(InteractionStack.Depth(_bb)).IsEqual(2);
	}

	[TestCase]
	public void Pop_ReturnsLastPushedEvent()
	{
		InteractionStack.Push(_bb, "window_closed", new Godot.Collections.Dictionary());
		InteractionStack.Push(_bb, "back_to_orbital_body", new Godot.Collections.Dictionary());

		var result = InteractionStack.Pop(_bb);
		AssertThat(result).IsEqual("back_to_orbital_body");
		AssertThat(InteractionStack.Depth(_bb)).IsEqual(1);

		result = InteractionStack.Pop(_bb);
		AssertThat(result).IsEqual("window_closed");
		AssertThat(InteractionStack.Depth(_bb)).IsEqual(0);
	}

	[TestCase]
	public void Pop_OnEmptyStack_ReturnsNull()
	{
		var result = InteractionStack.Pop(_bb);
		AssertThat(result).IsNull();
	}

	[TestCase]
	public void Peek_DoesNotRemoveEntry()
	{
		InteractionStack.Push(_bb, "window_closed", new Godot.Collections.Dictionary());

		var result = InteractionStack.Peek(_bb);
		AssertThat(result).IsEqual("window_closed");
		AssertThat(InteractionStack.Depth(_bb)).IsEqual(1);
	}

	[TestCase]
	public void Peek_OnEmptyStack_ReturnsNull()
	{
		var result = InteractionStack.Peek(_bb);
		AssertThat(result).IsNull();
	}

	// ───────── Clear ─────────

	[TestCase]
	public void Clear_WipesStack()
	{
		InteractionStack.Push(_bb, "a", new Godot.Collections.Dictionary());
		InteractionStack.Push(_bb, "b", new Godot.Collections.Dictionary());
		AssertThat(InteractionStack.Depth(_bb)).IsEqual(2);

		InteractionStack.Clear(_bb);
		AssertThat(InteractionStack.Depth(_bb)).IsEqual(0);
		AssertThat(InteractionStack.IsEmpty(_bb)).IsTrue();
	}

	// ───────── IsEmpty ─────────

	[TestCase]
	public void IsEmpty_TrueWhenNoEntries()
	{
		AssertThat(InteractionStack.IsEmpty(_bb)).IsTrue();
	}

	[TestCase]
	public void IsEmpty_FalseAfterPush()
	{
		InteractionStack.Push(_bb, "event", new Godot.Collections.Dictionary());
		AssertThat(InteractionStack.IsEmpty(_bb)).IsFalse();
	}

	// ───────── Snapshot / Restore ─────────

	[TestCase]
	public void SnapshotVars_CapturesExistingVars()
	{
		_bb.SetVar("SelectedBody", "Mars");
		_bb.SetVar("SelectedCell", 42);
		// "NonExistent" not set → should not appear in snapshot

		var snapshot = InteractionStack.SnapshotVars(_bb, "SelectedBody", "SelectedCell", "NonExistent");

		AssertThat(snapshot.ContainsKey("SelectedBody")).IsTrue();
		AssertThat(snapshot["SelectedBody"].AsString()).IsEqual("Mars");
		AssertThat(snapshot.ContainsKey("SelectedCell")).IsTrue();
		AssertThat(snapshot["SelectedCell"].AsInt32()).IsEqual(42);
		AssertThat(snapshot.ContainsKey("NonExistent")).IsFalse();
	}

	[TestCase]
	public void Pop_RestoresSnapshotToBlackboard()
	{
		// Push with a snapshot containing SelectedStation
		_bb.SetVar("SelectedStation", "StationAlpha");
		var snapshot = InteractionStack.SnapshotVars(_bb, "SelectedStation");

		InteractionStack.Push(_bb, "station_opened", snapshot);

		// Overwrite blackboard value to simulate the source state cleaning up
		_bb.SetVar("SelectedStation", default(Variant));

		// Pop should restore SelectedStation
		var returnEvent = InteractionStack.Pop(_bb);
		AssertThat(returnEvent).IsEqual("station_opened");
		AssertThat(_bb.GetVar("SelectedStation").AsString()).IsEqual("StationAlpha");
	}

	[TestCase]
	public void Pop_WithEmptySnapshot_DoesNotModifyBlackboard()
	{
		_bb.SetVar("ExistingKey", "KeepMe");

		InteractionStack.Push(_bb, "window_closed", new Godot.Collections.Dictionary());
		var result = InteractionStack.Pop(_bb);

		AssertThat(result).IsEqual("window_closed");
		AssertThat(_bb.GetVar("ExistingKey").AsString()).IsEqual("KeepMe");
	}

	// ───────── Multiple entries lifecycle ─────────

	[TestCase]
	public void FullNavigationSequence_HUD_to_Body_to_Station_to_Building()
	{
		// Simulate: HUD → OrbitalBody → Station → Building
		InteractionStack.Push(_bb, "window_closed", new Godot.Collections.Dictionary());

		_bb.SetVar("SelectedBody", "Earth");
		_bb.SetVar("SelectedContinent", "Eurasia");
		InteractionStack.Push(_bb, "back_to_orbital_body",
			InteractionStack.SnapshotVars(_bb, "SelectedBody", "SelectedContinent"));

		_bb.SetVar("SelectedStation", "StationAlpha");
		InteractionStack.Push(_bb, "station_opened",
			InteractionStack.SnapshotVars(_bb, "SelectedStation"));

		AssertThat(InteractionStack.Depth(_bb)).IsEqual(3);

		// Back from Building → Station
		var ev = InteractionStack.Pop(_bb);
		AssertThat(ev).IsEqual("station_opened");
		AssertThat(_bb.GetVar("SelectedStation").AsString()).IsEqual("StationAlpha");
		AssertThat(InteractionStack.Depth(_bb)).IsEqual(2);

		// Back from Station → OrbitalBody
		ev = InteractionStack.Pop(_bb);
		AssertThat(ev).IsEqual("back_to_orbital_body");
		AssertThat(_bb.GetVar("SelectedBody").AsString()).IsEqual("Earth");
		AssertThat(_bb.GetVar("SelectedContinent").AsString()).IsEqual("Eurasia");
		AssertThat(InteractionStack.Depth(_bb)).IsEqual(1);

		// Back from OrbitalBody → HUD
		ev = InteractionStack.Pop(_bb);
		AssertThat(ev).IsEqual("window_closed");
		AssertThat(InteractionStack.Depth(_bb)).IsEqual(0);
	}

	[TestCase]
	public void CloseToHUD_ClearsStack()
	{
		InteractionStack.Push(_bb, "a", new Godot.Collections.Dictionary());
		InteractionStack.Push(_bb, "b", new Godot.Collections.Dictionary());
		InteractionStack.Push(_bb, "c", new Godot.Collections.Dictionary());

		InteractionStack.Clear(_bb);
		AssertThat(InteractionStack.IsEmpty(_bb)).IsTrue();

		// Pop on cleared stack returns null
		var result = InteractionStack.Pop(_bb);
		AssertThat(result).IsNull();
	}
}
