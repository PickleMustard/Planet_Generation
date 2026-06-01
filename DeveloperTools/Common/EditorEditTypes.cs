#if DEBUG
using Godot;

namespace DeveloperTools.Common;

/// <summary>Editable required-resource slot (resource id + amount).</summary>
public sealed class EditorResourceAmount
{
    public string ResourceId { get; set; } = "";
    public int Amount { get; set; } = 1;
}

/// <summary>
/// Editable 2D icon block, mirroring the <c>icon:</c> YAML section
/// (base_path / scale / tint). Stores the raw base_path string so round-trips
/// preserve exactly what was authored (no texture loading / fallback).
/// </summary>
public sealed class EditorIcon
{
    public string? BasePath { get; set; }
    public float Scale { get; set; } = 1.0f;
    public Color Tint { get; set; } = Colors.White;
}

/// <summary>
/// Editable 3D visual block, mirroring the <c>visual:</c> YAML section.
/// Field set and defaults match VisualDefinition / BuildingEditor's VisualEdit.
/// </summary>
public sealed class EditorVisual
{
    public string? ModelPath { get; set; }
    public string? ModelMaterial { get; set; }
    public string? AnimationPath { get; set; }
    public float Scale { get; set; } = 1.0f;
    public Vector3 RotationOffset { get; set; } = Vector3.Zero;
    public string ShapeId { get; set; } = "hexagon";
    public float ShapeSize { get; set; } = 64f;
}
#endif
