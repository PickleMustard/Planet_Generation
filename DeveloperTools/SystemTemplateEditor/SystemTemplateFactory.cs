#if DEBUG
using Godot;
using GDict = Godot.Collections.Dictionary;

namespace DeveloperTools.SystemTemplateEditor;

/// <summary>
/// Builds fresh <see cref="BodyNode"/>s with loader-shape <see cref="GDict"/> defaults so newly
/// authored bodies round-trip through <see cref="SystemTemplateEditorYamlIO"/> unchanged. Child
/// category is derived from the parent: dominants take planetary children, planetary/satellite take
/// satellites, belts are leaves.
/// </summary>
public static class SystemTemplateFactory
{
    public static BodyNode NewDominant(string name)
    {
        var raw = new GDict
        {
            ["type"] = "Star",
            ["name"] = name,
            ["template"] = new GDict
            {
                ["mass"] = 500000.0f,
                ["size"] = 500.0f,
                ["position"] = new Godot.Collections.Array { 0.0f, 0.0f, 0.0f },
            },
        };
        return new BodyNode(raw, BodyCategory.Dominant);
    }

    public static BodyNode NewBelt(string name)
    {
        var raw = new GDict
        {
            ["type"] = "Asteroid",
            ["name"] = name,
            ["ring_apogee"] = 4000.0f,
            ["ring_perigee"] = 3500.0f,
            ["lower_range"] = 8,
            ["upper_range"] = 16,
            ["orbital_center_index"] = 0,
        };
        return new BodyNode(raw, BodyCategory.Belt);
    }

    public static BodyNode NewPlanetary(string name)
    {
        var raw = new GDict
        {
            ["type"] = "RockyPlanet",
            ["name"] = name,
            ["template"] = new GDict { ["mass"] = 1000.0f, ["size"] = 150.0f },
            ["orbital_parameters"] = new GDict
            {
                ["apogee"] = 2500.0f,
                ["perigee"] = 2000.0f,
                ["starting_angle"] = 0.0f,
                ["vertical_offset"] = 0.0f,
            },
        };
        return new BodyNode(raw, BodyCategory.Planetary);
    }

    public static BodyNode NewSatellite(string name)
    {
        var raw = new GDict
        {
            ["type"] = "Moon",
            ["name"] = name,
            ["template"] = new GDict
            {
                ["apogee"] = 600.0f,
                ["perigee"] = 400.0f,
                ["starting_angle"] = 0.0f,
                ["vertical_offset"] = 0.0f,
                ["mass"] = 80.0f,
                ["size"] = 40.0f,
            },
        };
        return new BodyNode(raw, BodyCategory.Satellite);
    }

    /// <summary>Creates a child appropriate for <paramref name="parent"/>, or null if it takes none.</summary>
    public static BodyNode? NewChildOf(BodyNode parent)
    {
        int n = parent.Children.Count + 1;
        return parent.Category switch
        {
            BodyCategory.Dominant => NewPlanetary($"{parent.Name}_planet_{n}"),
            BodyCategory.Planetary => NewSatellite($"{parent.Name}_moon_{n}"),
            BodyCategory.Satellite => NewSatellite($"{parent.Name}_moon_{n}"),
            _ => null,
        };
    }
}
#endif
