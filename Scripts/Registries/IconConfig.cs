using Godot;

namespace Registries;

/// <summary>
/// Godot-native wrapper resource for an icon texture. Stored as a <c>.tres</c> so the
/// exporter tracks (and keeps) the embedded <see cref="Texture2D"/>; configuration points
/// at the <c>.tres</c> path instead of a raw <c>.png</c>/<c>.svg</c> file.
/// </summary>
[GlobalClass]
public partial class IconConfig : Resource
{
    /// <summary>Identifier for the icon (matches the asset stem, e.g. <c>ore</c>).</summary>
    [Export]
    public StringName Id { get; set; } = "";

    /// <summary>The wrapped icon texture.</summary>
    [Export]
    public Texture2D? Texture { get; set; }

    /// <summary>Default scale multiplier for UI display.</summary>
    [Export]
    public float Scale { get; set; } = 1.0f;

    /// <summary>Default tint color. White = no tint.</summary>
    [Export]
    public Color Tint { get; set; } = Colors.White;

    /// <summary>Optional extra metadata.</summary>
    [Export]
    public Godot.Collections.Dictionary Details { get; set; } = new();
}
