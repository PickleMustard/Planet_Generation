using System.Collections.Generic;
using GdUnit4;
using static GdUnit4.Assertions;
using Constructables;
using Constructables.Tick;
using Structures.Logistics;

namespace Tests.Constructables.Tick;

[TestSuite]
public class ManufactureTickEngineTest
{
    // ========================================================================
    // PRIORITY ORDERING
    // ========================================================================

    [TestCase]
    public void TickOrder_RespectsPriority()
    {
        var engine = ManufactureTickEngine.CreateForTesting();
        var log = new List<string>();

        // Register in reverse priority order
        var low = new RecordingTickable(2, "low", log);
        var high = new RecordingTickable(0, "high", log);
        var mid = new RecordingTickable(1, "mid", log);

        engine.Register(low);
        engine.Register(high);
        engine.Register(mid);
        engine.SingleTickForTesting();

        AssertThat(log).ContainsExactly("high", "mid", "low");

        engine.Stop();
    }

    [TestCase]
    public void Building_DefaultPriority_IsZero()
    {
        var building = new Building();
        var tickable = (IManufactureTickable)building;

        AssertThat(tickable.TickPriority).IsEqual(0);
    }

    [TestCase]
    public void ResourceLink_Priority_IsOne()
    {
        var link = new ResourceLink();
        var tickable = (IManufactureTickable)link;

        AssertThat(tickable.TickPriority).IsEqual(1);
    }

    [TestCase]
    public void ResourceLink_TicksAfter_Building()
    {
        var engine = ManufactureTickEngine.CreateForTesting();
        var log = new List<string>();

        // Use mock tickables with the same priorities as the real classes
        var building = new RecordingTickable(0, "building", log);
        var link = new RecordingTickable(1, "link", log);

        // Register link first, then building — engine should still tick building first
        engine.Register(link);
        engine.Register(building);
        engine.SingleTickForTesting();

        AssertThat(log).ContainsExactly("building", "link");

        engine.Stop();
    }

    // ========================================================================
    // MOCK HELPERS
    // ========================================================================

    private class RecordingTickable : IManufactureTickable
    {
        public int TickPriority { get; }
        public string Name { get; }
        public List<string> Log { get; }

        public RecordingTickable(int priority, string name, List<string> log)
        {
            TickPriority = priority;
            Name = name;
            Log = log;
        }

        public void OnManufactureTick(float delta)
        {
            Log.Add(Name);
        }
    }
}
