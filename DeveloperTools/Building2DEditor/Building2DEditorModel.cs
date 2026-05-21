#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Structures.Resources;
using UtilityLibrary.DataLoading;

namespace DeveloperTools.Building2DEditor;

/// <summary>
/// In-memory edit state for the Building2DEditor. Loaded from
/// res://Configuration/Building2D/*.yaml and written back via
/// <see cref="Building2DEditorYamlIO"/>.
/// </summary>
public class Building2DEditorModel
{
    public class SideEdit
    {
        public List<BuildingShape2D.SlotSpec> Slots { get; set; } = new();
    }

    public class ShapeEditEntry
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public List<Vector2> Vertices { get; set; } = new();
        public List<SideEdit> Sides { get; set; } = new();
        public string SourceFilePath { get; set; } = "";
        public bool IsNew { get; set; }
        public bool IsDirty { get; set; }

        public void EnsureSideCount()
        {
            while (Sides.Count < Vertices.Count) Sides.Add(new SideEdit());
            if (Sides.Count > Vertices.Count)
                Sides.RemoveRange(Vertices.Count, Sides.Count - Vertices.Count);
        }
    }

    private readonly string _shapesDirectory;
    private readonly List<ShapeEditEntry> _shapes = new();
    private readonly HashSet<string> _loadedSourceFiles = new();

    public IReadOnlyList<ShapeEditEntry> Shapes => _shapes;
    public string ShapesDirectory => _shapesDirectory;
    public IReadOnlySet<string> LoadedSourceFiles => _loadedSourceFiles;
    public bool HasUnsavedChanges => _shapes.Any(s => s.IsNew || s.IsDirty);

    public Building2DEditorModel(string shapesDirectory)
    {
        ArgumentNullException.ThrowIfNull(shapesDirectory);
        _shapesDirectory = shapesDirectory.TrimEnd('/') + "/";
    }

    public void LoadFromDisk()
    {
        _shapes.Clear();
        _loadedSourceFiles.Clear();

        if (!DirAccess.DirExistsAbsolute(_shapesDirectory))
            DirAccess.MakeDirRecursiveAbsolute(_shapesDirectory);

        foreach (var file in DirAccess.GetFilesAt(_shapesDirectory))
        {
            if (!file.EndsWith(".yaml") && !file.EndsWith(".yml")) continue;
            string path = _shapesDirectory + file;
            _loadedSourceFiles.Add(path);
            var shape = BuildingShape2DLoader.LoadShape(path);
            if (shape == null) continue;

            var entry = new ShapeEditEntry
            {
                Id = shape.Id,
                DisplayName = shape.DisplayName,
                SourceFilePath = path,
                Vertices = shape.Vertices.ToList(),
            };
            foreach (var s in shape.Sides)
            {
                entry.Sides.Add(new SideEdit { Slots = CloneSlots(s.Slots) });
            }
            entry.EnsureSideCount();
            _shapes.Add(entry);
        }

        _shapes.Sort((a, b) => string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase));
    }

    private static List<BuildingShape2D.SlotSpec> CloneSlots(IEnumerable<BuildingShape2D.SlotSpec> source)
    {
        var copy = new List<BuildingShape2D.SlotSpec>();
        foreach (var s in source)
            copy.Add(new BuildingShape2D.SlotSpec { Kind = s.Kind, StateOfMatter = s.StateOfMatter });
        return copy;
    }

    public ShapeEditEntry AddNewShape(string id)
    {
        var entry = new ShapeEditEntry
        {
            Id = id,
            DisplayName = id,
            IsNew = true,
            IsDirty = true,
            SourceFilePath = $"{_shapesDirectory}{id}.yaml",
            Vertices = new List<Vector2>
            {
                new(0.0f, -1.0f),
                new(0.866f, -0.5f),
                new(0.866f, 0.5f),
                new(0.0f, 1.0f),
                new(-0.866f, 0.5f),
                new(-0.866f, -0.5f),
            },
        };
        entry.EnsureSideCount();
        _shapes.Add(entry);
        return entry;
    }

    public ShapeEditEntry DuplicateShape(ShapeEditEntry source, string newId)
    {
        var entry = new ShapeEditEntry
        {
            Id = newId,
            DisplayName = source.DisplayName,
            IsNew = true,
            IsDirty = true,
            SourceFilePath = $"{_shapesDirectory}{newId}.yaml",
            Vertices = new List<Vector2>(source.Vertices),
        };
        foreach (var s in source.Sides)
        {
            entry.Sides.Add(new SideEdit { Slots = CloneSlots(s.Slots) });
        }
        entry.EnsureSideCount();
        _shapes.Add(entry);
        return entry;
    }

    public void DeleteShape(ShapeEditEntry entry)
    {
        _shapes.Remove(entry);
    }

    public bool IdExists(string id)
    {
        return _shapes.Any(s => string.Equals(s.Id, id, StringComparison.Ordinal));
    }
}
#endif
