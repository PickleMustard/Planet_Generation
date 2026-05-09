using GdUnit4;
using static GdUnit4.Assertions;
using Structures.Logistics;

namespace Tests.Structures.Logistics;

[TestSuite]
public class LinkProfileTest
{
    [TestCase]
    public void Creation_SetsAllProperties()
    {
        var profile = new LinkProfile
        {
            IdName = "conveyor_belt_mk1",
            Tier = 2,
            TransportSpeed = 0.25f,
            PackageSize = 50,
            BundleTime = 3,
            SlotCapacity = 4
        };

        AssertThat(profile.IdName).IsEqual("conveyor_belt_mk1");
        AssertThat(profile.Tier).IsEqual(2);
        AssertThat(profile.TransportSpeed).IsEqual(0.25f);
        AssertThat(profile.PackageSize).IsEqual(50);
        AssertThat(profile.BundleTime).IsEqual(3);
        AssertThat(profile.SlotCapacity).IsEqual(4);
    }

    [TestCase]
    public void DefaultValues_AreEmptyOrZero()
    {
        var profile = new LinkProfile();

        AssertThat(profile.IdName).IsEqual(string.Empty);
        AssertThat(profile.Tier).IsEqual(0);
        AssertThat(profile.TransportSpeed).IsEqual(0f);
        AssertThat(profile.PackageSize).IsEqual(0);
        AssertThat(profile.BundleTime).IsEqual(0);
        AssertThat(profile.SlotCapacity).IsEqual(0);
    }

    [TestCase]
    public void TransportSpeed_CanBeSetWithinRange()
    {
        var profile = new LinkProfile
        {
            TransportSpeed = 0.75f
        };

        AssertThat(profile.TransportSpeed).IsEqual(0.75f);
    }

    [TestCase]
    public void PackageSize_CanBePositive()
    {
        var profile = new LinkProfile
        {
            PackageSize = 100
        };

        AssertThat(profile.PackageSize).IsEqual(100);
    }
}
