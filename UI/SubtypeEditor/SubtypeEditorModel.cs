using System;
using System.Collections.Generic;
using System.Linq;
using ProceduralGeneration.SubtypeSystem;
using Structures.Resources;
using UtilityLibrary;
using UtilityLibrary.DataLoading;

namespace UI.SubtypeEditor;

/// <summary>
/// Thin write-through wrapper around <see cref="SubtypeDatabase"/> for the Planet Generator
/// subtype editor. The database itself is the source of truth in memory; the model layers a
/// per-id dirty set on top and routes saves through <see cref="SubtypeYamlIO"/>.
/// </summary>
public class SubtypeEditorModel
{
    private readonly HashSet<string> _dirty = new(StringComparer.Ordinal);
    private readonly HashSet<string> _isNew = new(StringComparer.Ordinal);

    public event Action? RegistryChanged;
    public event Action<string>? DefinitionChanged;

    public IReadOnlyCollection<SubtypeDefinition> All => SubtypeDatabase.Instance.All;

    public IEnumerable<SubtypeDefinition> ByFamily(BodyFamily family) =>
        SubtypeDatabase.Instance.ByFamily(family);

    public SubtypeDefinition? GetById(string id) => SubtypeDatabase.Instance.GetById(id);

    public bool Exists(string id) => SubtypeDatabase.Instance.Exists(id);

    public bool HasUnsavedChanges => _dirty.Count > 0 || _isNew.Count > 0;

    public bool IsDirty(string id) => _dirty.Contains(id) || _isNew.Contains(id);

    /// <summary>Reload database from disk and clear dirty state.</summary>
    public void LoadFromDisk()
    {
        var loaded = SubtypeDefinitionLoader.LoadAll();
        if (loaded == null)
        {
            GameLogger.Warning("SubtypeEditorModel: SubtypeDefinitionLoader.LoadAll returned null");
            return;
        }
        SubtypeDatabase.Instance.ReplaceInMemory(loaded);
        _dirty.Clear();
        _isNew.Clear();
        RegistryChanged?.Invoke();
    }

    /// <summary>Persist current database contents to disk via <see cref="SubtypeYamlIO"/>.</summary>
    public void Save()
    {
        SubtypeYamlIO.WriteAll(SubtypeDatabase.Instance.All);
        _dirty.Clear();
        _isNew.Clear();
        RegistryChanged?.Invoke();
    }

    // ── CRUD ────────────────────────────────────────────────────────────

    public bool Add(string id, string displayName, BodyFamily family)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (SubtypeDatabase.Instance.Exists(id)) return false;
        var def = new SubtypeDefinition
        {
            Id = id,
            DisplayName = string.IsNullOrEmpty(displayName) ? id : displayName,
            Family = family,
        };
        SubtypeDatabase.Instance.Add(def);
        _isNew.Add(id);
        RegistryChanged?.Invoke();
        return true;
    }

    public bool Remove(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        if (!SubtypeDatabase.Instance.Remove(id)) return false;
        _dirty.Add(id);
        _isNew.Remove(id);
        RegistryChanged?.Invoke();
        return true;
    }

    public bool Rename(string oldId, string newId)
    {
        if (string.IsNullOrEmpty(oldId) || string.IsNullOrEmpty(newId)) return false;
        if (oldId == newId) return false;
        if (SubtypeDatabase.Instance.Exists(newId)) return false;
        var def = SubtypeDatabase.Instance.GetById(oldId);
        if (def == null) return false;
        SubtypeDatabase.Instance.Rename(oldId, newId);
        if (_isNew.Remove(oldId)) _isNew.Add(newId);
        _dirty.Add(oldId);
        _dirty.Add(newId);
        RegistryChanged?.Invoke();
        return true;
    }

    /// <summary>Mark a subtype dirty after in-place mutation of its fields.</summary>
    public void MarkDirty(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (!SubtypeDatabase.Instance.Exists(id)) return;
        _dirty.Add(id);
        DefinitionChanged?.Invoke(id);
    }

    public IReadOnlyList<string> FindReferences(string subtypeId)
    {
        // Plan: cascade-delete scan only catches biome color-overrides today.
        // The Planet Generator scene does not own biomes, so just return empty.
        return Array.Empty<string>();
    }
}
