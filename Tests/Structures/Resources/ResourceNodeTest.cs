using GdUnit4;
using static GdUnit4.Assertions;
using Structures.Logistics;
using Structures.Resources;

namespace Tests.Structures.Resources;

[TestSuite]
public class ResourceNodeTest
{
    // ========================================================================
    // PROPERTY DEFAULTS
    // ========================================================================

    [TestCase]
    public void DefaultValues_AreNullOrDefault()
    {
        var node = new ResourceNode();

        AssertThat(node.Owner).IsNull();
        AssertThat(node.SideIndex).IsEqual(0);
        AssertThat(node.SlotIndex).IsEqual(0);
        AssertThat(node.Kind).IsEqual(ResourceNodeKind.Import);
        AssertThat(node.Link).IsNull();
    }

    // ========================================================================
    // LINK PROPERTY GETTER / SETTER
    // ========================================================================

    [TestCase]
    public void Link_CanSetAndRetrieve()
    {
        var node = new ResourceNode();
        var link = new ResourceLink();

        node.Link = link;

        AssertThat(node.Link).IsEqual(link);
    }

    [TestCase]
    public void Link_SetToNull_RetrievesNull()
    {
        var node = new ResourceNode();
        var link = new ResourceLink();

        node.Link = link;
        node.Link = null;

        AssertThat(node.Link).IsNull();
    }

    // ========================================================================
    // CAN CONNECT TO
    // ========================================================================

    [TestCase]
    public void CanConnectTo_ImportToExport_ReturnsTrue()
    {
        var import = new ResourceNode { Kind = ResourceNodeKind.Import };
        var export = new ResourceNode { Kind = ResourceNodeKind.Export };

        AssertThat(import.CanConnectTo(export)).IsTrue();
    }

    [TestCase]
    public void CanConnectTo_ExportToImport_ReturnsTrue()
    {
        var export = new ResourceNode { Kind = ResourceNodeKind.Export };
        var import = new ResourceNode { Kind = ResourceNodeKind.Import };

        AssertThat(export.CanConnectTo(import)).IsTrue();
    }

    [TestCase]
    public void CanConnectTo_FlexToAnything_ReturnsTrue()
    {
        var flex = new ResourceNode { Kind = ResourceNodeKind.Flex };
        var import = new ResourceNode { Kind = ResourceNodeKind.Import };
        var export = new ResourceNode { Kind = ResourceNodeKind.Export };
        var otherFlex = new ResourceNode { Kind = ResourceNodeKind.Flex };

        AssertThat(flex.CanConnectTo(import)).IsTrue();
        AssertThat(flex.CanConnectTo(export)).IsTrue();
        AssertThat(flex.CanConnectTo(otherFlex)).IsTrue();
    }

    [TestCase]
    public void CanConnectTo_ImportToImport_ReturnsFalse()
    {
        var a = new ResourceNode { Kind = ResourceNodeKind.Import };
        var b = new ResourceNode { Kind = ResourceNodeKind.Import };

        AssertThat(a.CanConnectTo(b)).IsFalse();
    }

    [TestCase]
    public void CanConnectTo_ExportToExport_ReturnsFalse()
    {
        var a = new ResourceNode { Kind = ResourceNodeKind.Export };
        var b = new ResourceNode { Kind = ResourceNodeKind.Export };

        AssertThat(a.CanConnectTo(b)).IsFalse();
    }

    // ========================================================================
    // CONNECT VALIDATION
    // ========================================================================

    [TestCase]
    public void Connect_WhenNodeIsSource_Succeeds()
    {
        var node = new ResourceNode();
        var other = new ResourceNode();
        var link = new ResourceLink { Source = node, Target = other };

        node.Connect(link);

        AssertThat(node.Link).IsEqual(link);
    }

    [TestCase]
    public void Connect_WhenNodeIsTarget_Succeeds()
    {
        var node = new ResourceNode();
        var other = new ResourceNode();
        var link = new ResourceLink { Source = other, Target = node };

        node.Connect(link);

        AssertThat(node.Link).IsEqual(link);
    }

    [TestCase]
    public void Connect_WhenNodeIsNeitherSourceNorTarget_Throws()
    {
        var node = new ResourceNode();
        var a = new ResourceNode();
        var b = new ResourceNode();
        var link = new ResourceLink { Source = a, Target = b };

        AssertThrown(() => node.Connect(link))
            .IsInstanceOf<System.ArgumentException>();
    }

    // ========================================================================
    // CONNECT / DISCONNECT LIFECYCLE
    // ========================================================================

    [TestCase]
    public void Disconnect_ClearsOwnLinkReference()
    {
        var node = new ResourceNode();
        var other = new ResourceNode();
        var link = new ResourceLink { Source = node, Target = other };

        node.Connect(link);
        node.Disconnect();

        AssertThat(node.Link).IsNull();
    }

    [TestCase]
    public void Disconnect_NullsLinkSource_WhenNodeWasSource()
    {
        var source = new ResourceNode();
        var target = new ResourceNode();
        var link = new ResourceLink { Source = source, Target = target };

        source.Connect(link);
        source.Disconnect();

        AssertThat(link.Source).IsNull();
        AssertThat(link.Target).IsEqual(target);
    }

    [TestCase]
    public void Disconnect_NullsLinkTarget_WhenNodeWasTarget()
    {
        var source = new ResourceNode();
        var target = new ResourceNode();
        var link = new ResourceLink { Source = source, Target = target };

        target.Connect(link);
        target.Disconnect();

        AssertThat(link.Target).IsNull();
        AssertThat(link.Source).IsEqual(source);
    }

    [TestCase]
    public void Disconnect_WhenAlreadyDisconnected_IsNoOp()
    {
        var node = new ResourceNode();

        // Should not throw
        node.Disconnect();

        AssertThat(node.Link).IsNull();
    }

    [TestCase]
    public void Disconnect_WithStaleLink_DoesNotAffectOtherNode()
    {
        var node = new ResourceNode();
        var other = new ResourceNode();
        var link = new ResourceLink { Source = node, Target = other };

        node.Connect(link);
        // Simulate stale connection: node still points to old link,
        // but link source/target were nulled elsewhere.
        link.Source = null;
        link.Target = null;

        // Disconnecting the node should not throw even though link is stale
        node.Disconnect();

        AssertThat(node.Link).IsNull();
    }

    // ========================================================================
    // INTEGRATION WITH ResourceLink.Disconnect
    // ========================================================================

    [TestCase]
    public void ResourceLinkDisconnect_ClearsTypedLinkOnSourceAndTarget()
    {
        var source = new ResourceNode();
        var target = new ResourceNode();
        var link = new ResourceLink { Source = source, Target = target };

        source.Link = link;
        target.Link = link;

        link.Disconnect();

        AssertThat(source.Link).IsNull();
        AssertThat(target.Link).IsNull();
    }

    [TestCase]
    public void FullLifecycle_ConnectThenResourceLinkDisconnect()
    {
        var source = new ResourceNode { Kind = ResourceNodeKind.Export };
        var target = new ResourceNode { Kind = ResourceNodeKind.Import };
        var link = new ResourceLink { Source = source, Target = target };

        source.Connect(link);
        target.Connect(link);

        AssertThat(source.Link).IsEqual(link);
        AssertThat(target.Link).IsEqual(link);

        link.Disconnect();

        AssertThat(link.Source).IsNull();
        AssertThat(link.Target).IsNull();
        AssertThat(source.Link).IsNull();
        AssertThat(target.Link).IsNull();
    }

    [TestCase]
    public void FullLifecycle_ConnectThenNodeDisconnect()
    {
        var source = new ResourceNode { Kind = ResourceNodeKind.Export };
        var target = new ResourceNode { Kind = ResourceNodeKind.Import };
        var link = new ResourceLink { Source = source, Target = target };

        source.Connect(link);
        target.Connect(link);

        source.Disconnect();

        AssertThat(source.Link).IsNull();
        AssertThat(link.Source).IsNull();
        AssertThat(target.Link).IsEqual(link); // target still connected
    }
}
