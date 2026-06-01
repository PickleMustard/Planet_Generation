using GdUnit4;
using static GdUnit4.Assertions;
using Constructables;
using Structures.Enums;
using Structures.Logistics;
using Structures.Resources;

namespace Tests.Structures.Logistics;

[TestSuite]
public class BuildingLinkServiceTest
{
    private static (Building b, ResourceNode node) MakeBuildingWithNode(
        ResourceNodeKind kind,
        string resourceId,
        StateOfMatter state = StateOfMatter.Solid)
    {
        var building = new Building();
        if (kind == ResourceNodeKind.Export || kind == ResourceNodeKind.Flex)
            building.OutputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource(resourceId)));
        if (kind == ResourceNodeKind.Import || kind == ResourceNodeKind.Flex)
            building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource(resourceId)));
        var node = new ResourceNode { Owner = building, Kind = kind, StateOfMatter = state };
        building.Nodes.Add(node);
        return (building, node);
    }

    [TestCase]
    public void TryConnect_ImportToExport_Succeeds()
    {
        BuildingLinkService.ResetForNewSession();
        var (_, exp) = MakeBuildingWithNode(ResourceNodeKind.Export, "iron");
        var (_, imp) = MakeBuildingWithNode(ResourceNodeKind.Import, "iron");
        var profile = new LinkProfile { TransportSpeed = 1f, PackageSize = 10, SlotCapacity = 4, BundleTime = 0 };

        bool ok = BuildingLinkService.Instance.TryConnect(exp, imp, profile, out var link);

        AssertThat(ok).IsTrue();
        AssertThat(link).IsNotNull();
        AssertThat(exp.Link).IsSame(link!);
        AssertThat(imp.Link).IsSame(link!);
    }

    [TestCase]
    public void TryConnect_SameKindNonFlex_Fails()
    {
        BuildingLinkService.ResetForNewSession();
        var (_, a) = MakeBuildingWithNode(ResourceNodeKind.Export, "iron");
        var (_, b) = MakeBuildingWithNode(ResourceNodeKind.Export, "iron");
        var profile = new LinkProfile { TransportSpeed = 1f, PackageSize = 10, SlotCapacity = 4, BundleTime = 0 };

        bool ok = BuildingLinkService.Instance.TryConnect(a, b, profile, out var link);

        AssertThat(ok).IsFalse();
        AssertThat(link).IsNull();
    }

    [TestCase]
    public void TryConnect_NodeAlreadyLinked_Fails()
    {
        BuildingLinkService.ResetForNewSession();
        var (_, exp) = MakeBuildingWithNode(ResourceNodeKind.Export, "iron");
        var (_, imp1) = MakeBuildingWithNode(ResourceNodeKind.Import, "iron");
        var (_, imp2) = MakeBuildingWithNode(ResourceNodeKind.Import, "iron");
        var profile = new LinkProfile { TransportSpeed = 1f, PackageSize = 10, SlotCapacity = 4, BundleTime = 0 };

        AssertThat(BuildingLinkService.Instance.TryConnect(exp, imp1, profile, out _)).IsTrue();
        AssertThat(BuildingLinkService.Instance.TryConnect(exp, imp2, profile, out _)).IsFalse();
    }

    [TestCase]
    public void Disconnect_RemovesLinkFromNodes()
    {
        BuildingLinkService.ResetForNewSession();
        var (_, exp) = MakeBuildingWithNode(ResourceNodeKind.Export, "iron");
        var (_, imp) = MakeBuildingWithNode(ResourceNodeKind.Import, "iron");
        var profile = new LinkProfile { TransportSpeed = 1f, PackageSize = 10, SlotCapacity = 4, BundleTime = 0 };
        BuildingLinkService.Instance.TryConnect(exp, imp, profile, out var link);

        BuildingLinkService.Instance.Disconnect(link!);

        AssertThat(exp.Link).IsNull();
        AssertThat(imp.Link).IsNull();
    }

    [TestCase]
    public void TryConnect_FlexToImport_Succeeds()
    {
        BuildingLinkService.ResetForNewSession();
        var (_, flex) = MakeBuildingWithNode(ResourceNodeKind.Flex, "iron");
        var (_, imp) = MakeBuildingWithNode(ResourceNodeKind.Import, "iron");
        var profile = new LinkProfile { TransportSpeed = 1f, PackageSize = 10, SlotCapacity = 4, BundleTime = 0 };

        bool ok = BuildingLinkService.Instance.TryConnect(flex, imp, profile, out var link);

        AssertThat(ok).IsTrue();
        AssertThat(link).IsNotNull();
    }

    [TestCase]
    public void TryConnect_StateMismatch_Fails()
    {
        BuildingLinkService.ResetForNewSession();
        var (_, exp) = MakeBuildingWithNode(ResourceNodeKind.Export, "iron", StateOfMatter.Solid);
        var (_, imp) = MakeBuildingWithNode(ResourceNodeKind.Import, "iron", StateOfMatter.Fluid);
        var profile = new LinkProfile
        {
            TransportSpeed = 1f, PackageSize = 10, SlotCapacity = 4, BundleTime = 0,
            StateOfMatter = StateOfMatter.Solid,
        };

        bool ok = BuildingLinkService.Instance.TryConnect(exp, imp, profile, out var link);

        AssertThat(ok).IsFalse();
        AssertThat(link).IsNull();
    }

    [TestCase]
    public void TryConnect_FluidLinkMatchesFluidNodes_Succeeds()
    {
        BuildingLinkService.ResetForNewSession();
        var (_, exp) = MakeBuildingWithNode(ResourceNodeKind.Export, "water", StateOfMatter.Fluid);
        var (_, imp) = MakeBuildingWithNode(ResourceNodeKind.Import, "water", StateOfMatter.Fluid);
        var profile = new LinkProfile
        {
            TransportSpeed = 1f, PackageSize = 10, SlotCapacity = 4, BundleTime = 0,
            StateOfMatter = StateOfMatter.Fluid,
        };

        bool ok = BuildingLinkService.Instance.TryConnect(exp, imp, profile, out var link);

        AssertThat(ok).IsTrue();
        AssertThat(link).IsNotNull();
    }
}
