using Godot;
using Structures.Enums;

namespace Structures.Resources;

public partial class NodeSpec : Resource
{
    public int SideIndex { get; set; }
    public int SlotIndex { get; set; }
    public ResourceNodeKind Kind { get; set; }
    public StateOfMatter StateOfMatter { get; set; }

    public ResourceNode Build(Constructables.Building owner)
    {
        return new ResourceNode
        {
            Owner = owner,
            SideIndex = SideIndex,
            SlotIndex = SlotIndex,
            Kind = Kind,
            StateOfMatter = StateOfMatter,
            Link = null,
        };
    }
}
