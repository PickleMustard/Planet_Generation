using GdUnit4;
using static GdUnit4.Assertions;
using Structures.Logistics;
using Structures.Resources;
using Constructables;

namespace Tests.Structures.Logistics;

[TestSuite]
public class ResourcePackageTest
{
    [TestCase]
    public void Creation_SetsProperties()
    {
        var package = new ResourcePackage
        {
            ResourceId = "iron_ore",
            Quantity = 42f,
            Progress = 0.5f,
            Stuck = true
        };

        AssertThat(package.ResourceId).IsEqual("iron_ore");
        AssertThat(package.Quantity).IsEqual(42f);
        AssertThat(package.Progress).IsEqual(0.5f);
        AssertThat(package.Stuck).IsTrue();
        AssertThat(package.Link).IsNull();
    }

    [TestCase]
    public void DefaultValues_AreEmptyOrZero()
    {
        var package = new ResourcePackage();

        AssertThat(package.ResourceId).IsEqual(string.Empty);
        AssertThat(package.Quantity).IsEqual(0f);
        AssertThat(package.Progress).IsEqual(0f);
        AssertThat(package.Stuck).IsFalse();
        AssertThat(package.IsComplete).IsFalse();
    }

    // ========================================================================
    // PROGRESS LOGIC
    // ========================================================================

    [TestCase]
    public void AdvanceProgress_IncrementsProgress()
    {
        var package = new ResourcePackage();
        package.AdvanceProgress(0.3f);

        AssertThat(package.Progress).IsEqual(0.3f);
    }

    [TestCase]
    public void AdvanceProgress_ClampsToMaxOfOne()
    {
        var package = new ResourcePackage();
        package.AdvanceProgress(1.5f);

        AssertThat(package.Progress).IsEqual(1.0f);
    }

    [TestCase]
    public void AdvanceProgress_ClampsToMinOfZero()
    {
        var package = new ResourcePackage { Progress = 0.5f };
        package.AdvanceProgress(-2.0f);

        AssertThat(package.Progress).IsEqual(0.0f);
    }

    [TestCase]
    public void Progress_Setter_ClampsToRange()
    {
        var package = new ResourcePackage { Progress = 2.0f };
        AssertThat(package.Progress).IsEqual(1.0f);

        package.Progress = -0.5f;
        AssertThat(package.Progress).IsEqual(0.0f);
    }

    [TestCase]
    public void IsComplete_True_WhenProgressReachesOne()
    {
        var package = new ResourcePackage { Progress = 1.0f };
        AssertThat(package.IsComplete).IsTrue();
    }

    [TestCase]
    public void IsComplete_False_WhenProgressBelowOne()
    {
        var package = new ResourcePackage { Progress = 0.99f };
        AssertThat(package.IsComplete).IsFalse();
    }

    // ========================================================================
    // TRY DEPOSIT
    // ========================================================================

    [TestCase]
    public void TryDeposit_NullLink_ReturnsFalse()
    {
        var package = new ResourcePackage { ResourceId = "iron_ore", Quantity = 10f };

        bool result = package.TryDeposit();

        AssertThat(result).IsFalse();
    }

    [TestCase]
    public void TryDeposit_NullTarget_ReturnsFalse()
    {
        var link = new ResourceLink();
        var package = new ResourcePackage
        {
            ResourceId = "iron_ore",
            Quantity = 10f,
            Link = link
        };

        bool result = package.TryDeposit();

        AssertThat(result).IsFalse();
    }

    [TestCase]
    public void TryDeposit_NullOwner_ReturnsFalse()
    {
        var target = new ResourceNode();
        var link = new ResourceLink { Target = target };
        var package = new ResourcePackage
        {
            ResourceId = "iron_ore",
            Quantity = 10f,
            Link = link
        };

        bool result = package.TryDeposit();

        AssertThat(result).IsFalse();
    }

    [TestCase]
    public void TryDeposit_ValidLink_DelegatesToOwner()
    {
        var building = new Building();
        building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("iron_ore")));
        var target = new ResourceNode { Owner = building };
        var link = new ResourceLink { Target = target };
        var package = new ResourcePackage
        {
            ResourceId = "iron_ore",
            Quantity = 25f,
            Link = link
        };

        bool result = package.TryDeposit();

        AssertThat(result).IsTrue();
        AssertThat(building.InputStorage.GetQuantity("iron_ore") > 0).IsTrue();
        AssertThat(building.InputStorage.GetQuantity("iron_ore")).IsEqual(25f);
    }

    [TestCase]
    public void TryDeposit_MultiplePackages_AccumulateInInputStorage()
    {
        var building = new Building();
        building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource("copper_ore")));
        var target = new ResourceNode { Owner = building };
        var link = new ResourceLink { Target = target };

        var package1 = new ResourcePackage
        {
            ResourceId = "copper_ore",
            Quantity = 10f,
            Link = link
        };
        var package2 = new ResourcePackage
        {
            ResourceId = "copper_ore",
            Quantity = 15f,
            Link = link
        };

        bool result1 = package1.TryDeposit();
        bool result2 = package2.TryDeposit();

        AssertThat(result1).IsTrue();
        AssertThat(result2).IsTrue();
        AssertThat(building.InputStorage.GetQuantity("copper_ore")).IsEqual(25f);
    }
}
