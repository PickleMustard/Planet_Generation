using System;
using Godot;
using UtilityLibrary;

namespace Structures.Resources;

/// <summary>
/// Lazily loads generic default PackedScene prototypes for each constructable type.
/// Used as fallback visuals when a definition's VisualDefinition has no model prototype.
/// </summary>
public static class DefaultModelRegistry
{
    private const string StationPath = "res://Prefabs/Defaults/DefaultStationModel.tscn";
    private const string UnitPath = "res://Prefabs/Defaults/DefaultUnitModel.tscn";
    private const string BuildingPath = "res://Prefabs/Defaults/DefaultBuildingModel.tscn";

    private static readonly Lazy<PackedScene?> _station = new(() => LoadScene(StationPath));
    private static readonly Lazy<PackedScene?> _unit = new(() => LoadScene(UnitPath));
    private static readonly Lazy<PackedScene?> _building = new(() => LoadScene(BuildingPath));

    public static PackedScene? GetStationDefault() => _station.Value;
    public static PackedScene? GetUnitDefault() => _unit.Value;
    public static PackedScene? GetBuildingDefault() => _building.Value;

    public static Node3D? InstantiateStationDefault() => Instantiate(_station.Value, StationPath);
    public static Node3D? InstantiateUnitDefault() => Instantiate(_unit.Value, UnitPath);
    public static Node3D? InstantiateBuildingDefault() => Instantiate(_building.Value, BuildingPath);

    private static PackedScene? LoadScene(string path)
    {
        var scene = GD.Load<PackedScene>(path);
        if (scene == null)
            GameLogger.Error($"DefaultModelRegistry: Failed to load '{path}'");
        return scene;
    }

    private static Node3D? Instantiate(PackedScene? scene, string path)
    {
        if (scene == null || !scene.CanInstantiate())
        {
            GameLogger.Warning($"DefaultModelRegistry: Scene '{path}' cannot be instantiated");
            return null;
        }
        return scene.Instantiate<Node3D>();
    }
}
