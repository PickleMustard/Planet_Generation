using GdUnit4;
using static GdUnit4.Assertions;
using Structures.Logistics;
using Structures.Resources;
using Constructables;
using Constructables.Tick;

namespace Tests.Structures.Logistics;

[TestSuite]
public class ResourceLinkTest
{
    // ========================================================================
    // CREATION & DEFAULTS
    // ========================================================================

    [TestCase]
    public void DefaultValues_AreEmptyOrZero()
    {
        var link = new ResourceLink();

        AssertThat(link.Source).IsNull();
        AssertThat(link.Target).IsNull();
        AssertThat(link.Profile).IsNull();
        AssertThat(link.InFlight).HasSize(0);
        AssertThat(link.BundleTimer).IsEqual(0);
        AssertThat(link.ArrivalBuffer).HasSize(0);
    }

    // ========================================================================
    // CONNECTION VALIDATION MATRIX
    // ========================================================================

    [TestCase]
    public void CanConnect_ImportToExport_ReturnsTrue()
    {
        var import = new ResourceNode { Kind = ResourceNodeKind.Import };
        var export = new ResourceNode { Kind = ResourceNodeKind.Export };

        AssertThat(ResourceLink.CanConnect(import, export)).IsTrue();
    }

    [TestCase]
    public void CanConnect_ExportToImport_ReturnsTrue()
    {
        var export = new ResourceNode { Kind = ResourceNodeKind.Export };
        var import = new ResourceNode { Kind = ResourceNodeKind.Import };

        AssertThat(ResourceLink.CanConnect(export, import)).IsTrue();
    }

    [TestCase]
    public void CanConnect_FlexToImport_ReturnsTrue()
    {
        var flex = new ResourceNode { Kind = ResourceNodeKind.Flex };
        var import = new ResourceNode { Kind = ResourceNodeKind.Import };

        AssertThat(ResourceLink.CanConnect(flex, import)).IsTrue();
    }

    [TestCase]
    public void CanConnect_FlexToExport_ReturnsTrue()
    {
        var flex = new ResourceNode { Kind = ResourceNodeKind.Flex };
        var export = new ResourceNode { Kind = ResourceNodeKind.Export };

        AssertThat(ResourceLink.CanConnect(flex, export)).IsTrue();
    }

    [TestCase]
    public void CanConnect_ImportToFlex_ReturnsTrue()
    {
        var import = new ResourceNode { Kind = ResourceNodeKind.Import };
        var flex = new ResourceNode { Kind = ResourceNodeKind.Flex };

        AssertThat(ResourceLink.CanConnect(import, flex)).IsTrue();
    }

    [TestCase]
    public void CanConnect_ExportToFlex_ReturnsTrue()
    {
        var export = new ResourceNode { Kind = ResourceNodeKind.Export };
        var flex = new ResourceNode { Kind = ResourceNodeKind.Flex };

        AssertThat(ResourceLink.CanConnect(export, flex)).IsTrue();
    }

    [TestCase]
    public void CanConnect_FlexToFlex_ReturnsTrue()
    {
        var flexA = new ResourceNode { Kind = ResourceNodeKind.Flex };
        var flexB = new ResourceNode { Kind = ResourceNodeKind.Flex };

        AssertThat(ResourceLink.CanConnect(flexA, flexB)).IsTrue();
    }

    [TestCase]
    public void CanConnect_ImportToImport_ReturnsFalse()
    {
        var a = new ResourceNode { Kind = ResourceNodeKind.Import };
        var b = new ResourceNode { Kind = ResourceNodeKind.Import };

        AssertThat(ResourceLink.CanConnect(a, b)).IsFalse();
    }

    [TestCase]
    public void CanConnect_ExportToExport_ReturnsFalse()
    {
        var a = new ResourceNode { Kind = ResourceNodeKind.Export };
        var b = new ResourceNode { Kind = ResourceNodeKind.Export };

        AssertThat(ResourceLink.CanConnect(a, b)).IsFalse();
    }

    [TestCase]
    public void CanConnect_NullNode_ReturnsFalse()
    {
        var node = new ResourceNode { Kind = ResourceNodeKind.Import };

        AssertThat(ResourceLink.CanConnect(null!, node)).IsFalse();
        AssertThat(ResourceLink.CanConnect(node, null!)).IsFalse();
        AssertThat(ResourceLink.CanConnect(null!, null!)).IsFalse();
    }

    // ========================================================================
    // TRY ENQUEUE
    // ========================================================================

    [TestCase]
    public void TryEnqueue_NullProfile_ReturnsFalse()
    {
        var link = new ResourceLink();
        AssertThat(link.TryEnqueue("iron", 10f)).IsFalse();
    }

    [TestCase]
    public void TryEnqueue_InvalidInput_ReturnsFalse()
    {
        var link = new ResourceLink
        {
            Profile = new LinkProfile
            {
                SlotCapacity = 2,
                BundleTime = 0,
                TransportSpeed = 0.1f,
                PackageSize = 50
            }
        };

        AssertThat(link.TryEnqueue("", 10f)).IsFalse();
        AssertThat(link.TryEnqueue("iron", 0f)).IsFalse();
        AssertThat(link.TryEnqueue("iron", -5f)).IsFalse();
    }

    [TestCase]
    public void TryEnqueue_WithinSlotCapacity_ReturnsTrue()
    {
        var link = new ResourceLink
        {
            Profile = new LinkProfile
            {
                SlotCapacity = 3,
                BundleTime = 0,
                TransportSpeed = 0.1f,
                PackageSize = 50
            }
        };

        AssertThat(link.TryEnqueue("iron", 10f)).IsTrue();
        AssertThat(link.InFlight).HasSize(1);
        AssertThat(link.InFlight[0].ResourceId).IsEqual("iron");
        AssertThat(link.InFlight[0].Quantity).IsEqual(10f);
    }

    [TestCase]
    public void TryEnqueue_AtSlotCapacity_ReturnsFalse()
    {
        var link = new ResourceLink
        {
            Profile = new LinkProfile
            {
                SlotCapacity = 2,
                BundleTime = 0,
                TransportSpeed = 0.1f,
                PackageSize = 50
            }
        };

        AssertThat(link.TryEnqueue("iron", 10f)).IsTrue();
        AssertThat(link.TryEnqueue("copper", 10f)).IsTrue();
        AssertThat(link.TryEnqueue("gold", 10f)).IsFalse();
        AssertThat(link.InFlight).HasSize(2);
    }

    [TestCase]
    public void TryEnqueue_SplitsLargeAmountsAcrossPackages()
    {
        var link = new ResourceLink
        {
            Profile = new LinkProfile
            {
                SlotCapacity = 3,
                BundleTime = 0,
                TransportSpeed = 0.1f,
                PackageSize = 10
            }
        };

        // 25 amount with package size 10 => 3 packages (10 + 10 + 5)
        AssertThat(link.TryEnqueue("iron", 25f)).IsTrue();
        AssertThat(link.InFlight).HasSize(3);
        AssertThat(link.InFlight[0].Quantity).IsEqual(10f);
        AssertThat(link.InFlight[1].Quantity).IsEqual(10f);
        AssertThat(link.InFlight[2].Quantity).IsEqual(5f);
    }

    [TestCase]
    public void TryEnqueue_PartialSplit_WhenSlotsLimited()
    {
        var link = new ResourceLink
        {
            Profile = new LinkProfile
            {
                SlotCapacity = 2,
                BundleTime = 0,
                TransportSpeed = 0.1f,
                PackageSize = 10
            }
        };

        // 25 amount, size 10, slots 2 => only 2 packages created (10 + 10)
        AssertThat(link.TryEnqueue("iron", 25f)).IsTrue();
        AssertThat(link.InFlight).HasSize(2);
        AssertThat(link.InFlight[0].Quantity).IsEqual(10f);
        AssertThat(link.InFlight[1].Quantity).IsEqual(10f);
    }

    // ========================================================================
    // TICK ADVANCEMENT
    // ========================================================================

    [TestCase]
    public void OnManufactureTick_AdvancesInFlightProgress()
    {
        var link = new ResourceLink
        {
            Profile = new LinkProfile
            {
                SlotCapacity = 2,
                BundleTime = 0,
                TransportSpeed = 0.25f,
                PackageSize = 50
            }
        };

        link.TryEnqueue("iron", 10f);
        AssertThat(link.InFlight[0].Progress).IsEqual(0f);

        link.OnManufactureTick(1f);
        AssertThat(link.InFlight[0].Progress).IsEqual(0.25f);

        link.OnManufactureTick(1f);
        AssertThat(link.InFlight[0].Progress).IsEqual(0.5f);
    }

    [TestCase]
    public void OnManufactureTick_StuckPackage_DoesNotAdvance()
    {
        var link = new ResourceLink
        {
            Profile = new LinkProfile
            {
                SlotCapacity = 1,
                BundleTime = 0,
                TransportSpeed = 0.5f,
                PackageSize = 50
            }
        };

        link.TryEnqueue("iron", 10f);
        link.InFlight[0].Stuck = true;

        link.OnManufactureTick(1f);
        AssertThat(link.InFlight[0].Progress).IsEqual(0f);
    }

    // ========================================================================
    // PACKAGE ARRIVAL + DEPOSIT SUCCESS
    // ========================================================================

    [TestCase]
    public void OnManufactureTick_PackageArrival_DepositSuccess_RemovesFromInFlight()
    {
        var building = new Building();
        building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron")));
        var target = new ResourceNode { Owner = building };
        var link = new ResourceLink
        {
            Target = target,
            Profile = new LinkProfile
            {
                SlotCapacity = 2,
                BundleTime = 0,
                TransportSpeed = 0.6f,
                PackageSize = 50
            }
        };

        link.TryEnqueue("iron", 25f);
        AssertThat(link.InFlight).HasSize(1);

        // Two ticks should complete the package (0.6 + 0.6 = 1.2 >= 1.0)
        link.OnManufactureTick(1f);
        link.OnManufactureTick(1f);

        AssertThat(link.InFlight).IsEmpty();
        AssertThat(link.ArrivalBuffer).IsEmpty();
        AssertThat(building.InputStorage.GetQuantity("iron")).IsEqual(25f);
    }

    [TestCase]
    public void OnManufactureTick_MultiplePackages_AccumulateInTargetStorage()
    {
        var building = new Building();
        building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("copper")));
        var target = new ResourceNode { Owner = building };
        var link = new ResourceLink
        {
            Target = target,
            Profile = new LinkProfile
            {
                SlotCapacity = 2,
                BundleTime = 0,
                TransportSpeed = 0.6f,
                PackageSize = 50
            }
        };

        link.TryEnqueue("copper", 10f);
        link.TryEnqueue("copper", 15f);

        link.OnManufactureTick(1f);
        link.OnManufactureTick(1f);

        AssertThat(link.InFlight).IsEmpty();
        AssertThat(building.InputStorage.GetQuantity("copper")).IsEqual(25f);
    }

    // ========================================================================
    // PACKAGE ARRIVAL + DEPOSIT FAILURE
    // ========================================================================

    [TestCase]
    public void OnManufactureTick_PackageArrival_DepositFailure_GoesToArrivalBuffer()
    {
        var target = new ResourceNode(); // No owner
        var link = new ResourceLink
        {
            Target = target,
            Profile = new LinkProfile
            {
                SlotCapacity = 2,
                BundleTime = 0,
                TransportSpeed = 0.6f,
                PackageSize = 50
            }
        };

        link.TryEnqueue("iron", 25f);
        link.OnManufactureTick(1f);
        link.OnManufactureTick(1f);

        AssertThat(link.InFlight).IsEmpty();
        AssertThat(link.ArrivalBuffer).HasSize(1);
        AssertThat(link.ArrivalBuffer[0].ResourceId).IsEqual("iron");
    }

    // ========================================================================
    // ARRIVAL BUFFER RETRY
    // ========================================================================

    [TestCase]
    public void OnManufactureTick_ArrivalBuffer_RetriesDepositWhenTimerFires()
    {
        var building = new Building();
        building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron")));
        var target = new ResourceNode(); // Initially no owner
        var link = new ResourceLink
        {
            Target = target,
            Profile = new LinkProfile
            {
                SlotCapacity = 1,
                BundleTime = 2,
                TransportSpeed = 0.6f,
                PackageSize = 50
            }
        };

        link.TryEnqueue("iron", 25f);
        link.OnManufactureTick(1f);
        link.OnManufactureTick(1f);

        // Package failed deposit and is now in ArrivalBuffer
        AssertThat(link.ArrivalBuffer).HasSize(1);

        // After tick 2: BundleTimer decremented from 2 to 1, no retry yet
        AssertThat(link.BundleTimer).IsEqual(1);

        // Now set an owner
        target.Owner = building;

        // Tick 3: BundleTimer 1 -> 0, retry fires at end of tick and resets to 2
        link.OnManufactureTick(1f);
        AssertThat(link.ArrivalBuffer).IsEmpty();
        AssertThat(building.InputStorage.GetQuantity("iron")).IsEqual(25f);
        AssertThat(link.BundleTimer).IsEqual(2);
    }

    [TestCase]
    public void OnManufactureTick_ArrivalBuffer_RetryEveryTick_WhenBundleTimeIsZero()
    {
        var building = new Building();
        building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron")));
        var target = new ResourceNode(); // Initially no owner
        var link = new ResourceLink
        {
            Target = target,
            Profile = new LinkProfile
            {
                SlotCapacity = 1,
                BundleTime = 0,
                TransportSpeed = 0.6f,
                PackageSize = 50
            }
        };

        link.TryEnqueue("iron", 25f);
        link.OnManufactureTick(1f);
        link.OnManufactureTick(1f);

        AssertThat(link.ArrivalBuffer).HasSize(1);

        // Set owner
        target.Owner = building;

        // With BundleTime = 0, retry should happen on the very next tick
        link.OnManufactureTick(1f);
        AssertThat(link.ArrivalBuffer).IsEmpty();
        AssertThat(building.InputStorage.GetQuantity("iron")).IsEqual(25f);
    }

    // ========================================================================
    // BUNDLE TIMER BEHAVIOUR
    // ========================================================================


    [TestCase]
    public void OnManufactureTick_BundleTimer_CountsDownAndResets()
    {
        var link = new ResourceLink
        {
            Profile = new LinkProfile
            {
                SlotCapacity = 1,
                BundleTime = 3,
                TransportSpeed = 0.1f,
                PackageSize = 50
            }
        };

        link.BundleTimer = 3;

        link.OnManufactureTick(1f);
        AssertThat(link.BundleTimer).IsEqual(2);

        link.OnManufactureTick(1f);
        AssertThat(link.BundleTimer).IsEqual(1);

        // Tick 3: decrements to 0, then resets to BundleTime at end of tick
        link.OnManufactureTick(1f);
        AssertThat(link.BundleTimer).IsEqual(3);

        // Continues counting down from reset value
        link.OnManufactureTick(1f);
        AssertThat(link.BundleTimer).IsEqual(2);
    }

    // ========================================================================
    // DISCONNECT
    // ========================================================================

    [TestCase]
    public void Disconnect_ClearsSourceAndTarget()
    {
        var source = new ResourceNode();
        var target = new ResourceNode();
        var link = new ResourceLink
        {
            Source = source,
            Target = target
        };
        source.Link = link;
        target.Link = link;

        link.Disconnect();

        AssertThat(link.Source).IsNull();
        AssertThat(link.Target).IsNull();
        AssertThat(source.Link).IsNull();
        AssertThat(target.Link).IsNull();
    }
}
