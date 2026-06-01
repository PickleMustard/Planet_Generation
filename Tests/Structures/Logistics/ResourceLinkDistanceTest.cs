using GdUnit4;
using Godot;
using static GdUnit4.Assertions;
using Constructables;
using Structures.GameState;
using Structures.Logistics;
using Structures.MeshGeneration;
using Structures.Resources;

namespace Tests.Structures.Logistics;

/// <summary>
/// Verifies link transport speed scales with the great-circle distance between the
/// source and target buildings' primary cells (per spec: longer links are slower).
/// </summary>
[TestSuite]
public class ResourceLinkDistanceTest
{
    private static VoronoiCell MakeCellAt(Vector3 center)
    {
        var p = new Point(center);
        var cell = new VoronoiCell(0, new[] { p }, System.Array.Empty<Triangle>(), System.Array.Empty<Edge>());
        cell.Center = center;
        return cell;
    }

    private static (Building b, ResourceNode export) MakeExportBuilding(VoronoiCell cell, string resourceId)
    {
        var building = new Building();
        building.OutputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource(resourceId)));
        building.SetPlacement(cell, null);
        var node = new ResourceNode { Owner = building, Kind = ResourceNodeKind.Export };
        building.Nodes.Add(node);
        return (building, node);
    }

    private static (Building b, ResourceNode import) MakeImportBuilding(VoronoiCell cell, string resourceId)
    {
        var building = new Building();
        building.InputStorage.AddSlot(new StorageSlot(SlotFilter.ForResource(resourceId)));
        building.SetPlacement(cell, null);
        var node = new ResourceNode { Owner = building, Kind = ResourceNodeKind.Import };
        building.Nodes.Add(node);
        return (building, node);
    }

    [TestCase]
    public void CellDistance_DefaultsToOne_WhenNoPlacement()
    {
        var link = new ResourceLink();
        AssertThat(link.CellDistance).IsEqual(1f);
    }

    [TestCase]
    public void CellDistance_ColocatedCells_IsOne()
    {
        var cell = MakeCellAt(new Vector3(1f, 0f, 0f));
        var (_, src) = MakeExportBuilding(cell, "iron");
        var (_, dst) = MakeImportBuilding(cell, "iron");

        var link = new ResourceLink { Profile = new LinkProfile { TransportSpeed = 1f, PackageSize = 100, SlotCapacity = 4, BundleTime = 0 } };
        link.ConnectNodes(src, dst);

        AssertThat(link.CellDistance).IsEqual(1f);
    }

    [TestCase]
    public void CellDistance_DistantCells_GreaterThanOne()
    {
        var nearCell = MakeCellAt(new Vector3(1f, 0f, 0f));
        var farCell  = MakeCellAt(new Vector3(0f, 0f, 1f)); // 90° away on unit sphere → π/2 ≈ 1.5708 rad

        var (_, src) = MakeExportBuilding(nearCell, "iron");
        var (_, dst) = MakeImportBuilding(farCell, "iron");

        var link = new ResourceLink { Profile = new LinkProfile { TransportSpeed = 1f, PackageSize = 100, SlotCapacity = 4, BundleTime = 0 } };
        link.ConnectNodes(src, dst);

        AssertThat(link.CellDistance).IsGreater(1.5f);
        AssertThat(link.CellDistance).IsLess(1.6f);
    }

    [TestCase]
    public void TransportSpeed_FarLink_DeliversSlowerThanNearLink()
    {
        var profile = new LinkProfile { TransportSpeed = 1f, PackageSize = 100, SlotCapacity = 4, BundleTime = 0 };

        // Near: same cell, distance=1
        var nearCell = MakeCellAt(new Vector3(1f, 0f, 0f));
        var (nearSrc, nearExport) = MakeExportBuilding(nearCell, "iron");
        var (_, nearImport) = MakeImportBuilding(nearCell, "iron");
        nearSrc.OutputStorage.Deposit("iron", 10);
        var nearLink = new ResourceLink { Profile = profile };
        nearLink.ConnectNodes(nearExport, nearImport);
        nearLink.TryEnqueue("iron", 10);

        // Far: orthogonal cells → distance ≈ π/2
        var srcCell = MakeCellAt(new Vector3(1f, 0f, 0f));
        var dstCell = MakeCellAt(new Vector3(0f, 0f, 1f));
        var (farSrc, farExport) = MakeExportBuilding(srcCell, "iron");
        var (_, farImport) = MakeImportBuilding(dstCell, "iron");
        farSrc.OutputStorage.Deposit("iron", 10);
        var farLink = new ResourceLink { Profile = profile };
        farLink.ConnectNodes(farExport, farImport);
        farLink.TryEnqueue("iron", 10);

        // Tick both with delta=1
        nearLink.OnManufactureTick(1f);
        farLink.OnManufactureTick(1f);

        // Near link: speed=1, distance=1 → progress=1 → delivered → InFlight count 0
        // Far link: speed=1/(π/2) ~ 0.637 → progress<1 → still in flight
        AssertThat(nearLink.InFlight.Count).IsEqual(0);
        AssertThat(farLink.InFlight.Count).IsEqual(1);
        AssertThat(farLink.InFlight[0].Progress).IsLess(1f);
    }
}
