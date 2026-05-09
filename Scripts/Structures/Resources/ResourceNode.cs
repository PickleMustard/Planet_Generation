using System;
using System;
using Godot;
using Structures.Logistics;

namespace Structures.Resources;

public enum ResourceNodeKind { Import, Export, Flex }

public enum BuildingSide { Top, Bottom, North, South, East, West }

public partial class ResourceNode : Resource
{
    public Constructables.Building? Owner { get; set; }
    public BuildingSide Side { get; set; }
    public Vector3 Position { get; set; }
    public ResourceNodeKind Kind { get; set; }

    private readonly object _linkLock = new();
    private ResourceLink? _link;

    /// <summary>
    /// The <see cref="ResourceLink"/> currently connected to this node.
    /// Thread-safe for read/write.
    /// </summary>
    public ResourceLink? Link
    {
        get
        {
            lock (_linkLock)
            {
                return _link;
            }
        }
        set
        {
            lock (_linkLock)
            {
                _link = value;
            }
        }
    }

    /// <summary>
    /// Checks whether this node can be connected to another node.
    /// Uses the same rules as <see cref="ResourceLink.CanConnect"/>.
    /// </summary>
    public bool CanConnectTo(ResourceNode other)
    {
        return ResourceLink.CanConnect(this, other);
    }

    /// <summary>
    /// Connects this node to the given <paramref name="link"/>.
    /// Validates that the link's source or target references this node.
    /// Thread-safe.
    /// </summary>
    /// <param name="link">The link to assign.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when this node is neither the source nor target of the link.
    /// </exception>
    public void Connect(ResourceLink link)
    {
        if (link.Source != this && link.Target != this)
        {
            throw new ArgumentException(
                "Link source/target must reference this node.", nameof(link));
        }

        lock (_linkLock)
        {
            _link = link;
        }
    }

    /// <summary>
    /// Disconnects this node from its current link.
    /// Clears this node's link reference and nulls the link's source/target
    /// if they point to this node. Thread-safe.
    /// </summary>
    public void Disconnect()
    {
        ResourceLink? current;

        lock (_linkLock)
        {
            current = _link;
            _link = null;
        }

        // Null out the link's source/target outside the lock to avoid deadlocks.
        if (current?.Source == this)
        {
            current.Source = null;
        }

        if (current?.Target == this)
        {
            current.Target = null;
        }
    }
}
