using Godot;

namespace Structures.Resources;

public partial class NodeSpec : Resource
{
    public BuildingSide Side { get; set; }
    public Vector3 Position { get; set; }
    public ResourceNodeKind Kind { get; set; }

    public ResourceNode Build(Constructables.Building owner)
    {
        return new ResourceNode
        {
            Owner = owner,
            Side = Side,
            Position = Position,
            Kind = Kind,
            Link = null,
        };
    }
}
