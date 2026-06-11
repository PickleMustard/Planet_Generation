#if DEBUG
using Godot;

namespace DeveloperTools.Common;

/// <summary>
/// Back-compat resource picker. The generic implementation now lives in
/// <see cref="EntityPickerPopup"/>; this thin subclass preserves the legacy
/// <see cref="ResourcePicked"/> signal so existing call sites need no changes.
/// New code should prefer <c>EntityPickers.Resource()</c> + <c>Picked</c>.
/// </summary>
public partial class ResourcePickerPopup : EntityPickerPopup
{
    [Signal]
    public delegate void ResourcePickedEventHandler(string resourceId);

    public static ResourcePickerPopup Create()
    {
        PackedScene? _scene = GD.Load<PackedScene>(
            "res://DeveloperTools/Common/ResourcePickerPopup.tscn"
        );
        ResourcePickerPopup _popup = _scene.Instantiate<ResourcePickerPopup>();
        return _popup;
    }

    public override void _Ready()
    {
        EntityPickers.ConfigureResource(this);
        Picked += id => EmitSignal(SignalName.ResourcePicked, id);
        base._Ready();
    }
}
#endif
