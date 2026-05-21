#if DEBUG
using System;
using System.Collections.Generic;
using System.Linq;
using Structures.Enums;
using Structures.Logistics;
using UtilityLibrary;
using UtilityLibrary.DataLoading;

namespace DeveloperTools.LinkProfileEditor;

/// <summary>
/// In-memory editable state for LinkProfile YAML. Reads via LinkConfigLoader,
/// writes via LinkProfileEditorYamlIO.
/// </summary>
public class LinkProfileEditorModel
{
    public class LinkProfileEditEntry
    {
        public string IdName { get; set; } = "";
        public int Tier { get; set; }
        public float TransportSpeed { get; set; }
        public int PackageSize { get; set; }
        public int BundleTime { get; set; }
        public int SlotCapacity { get; set; }
        public StateOfMatter StateOfMatter { get; set; } = StateOfMatter.Solid;
        public bool IsNew { get; set; }
        public bool IsDirty { get; set; }
    }

    private readonly string _configPath;
    private List<LinkProfileEditEntry> _entries = new();

    public IReadOnlyList<LinkProfileEditEntry> Entries => _entries;

    public bool HasUnsavedChanges => _entries.Any(e => e.IsNew || e.IsDirty);

    public LinkProfileEditorModel(string configPath)
    {
        ArgumentNullException.ThrowIfNull(configPath);
        _configPath = configPath;
    }

    public void LoadFromDisk()
    {
        var newEntries = new List<LinkProfileEditEntry>();
        var profiles = LinkConfigLoader.LoadLinkProfiles(_configPath);
        foreach (var profile in profiles)
        {
            newEntries.Add(new LinkProfileEditEntry
            {
                IdName = profile.IdName,
                Tier = profile.Tier,
                TransportSpeed = profile.TransportSpeed,
                PackageSize = profile.PackageSize,
                BundleTime = profile.BundleTime,
                SlotCapacity = profile.SlotCapacity,
                StateOfMatter = profile.StateOfMatter,
                IsNew = false,
                IsDirty = false,
            });
        }
        _entries = newEntries;
    }

    public LinkProfileEditEntry AddProfile()
    {
        var entry = new LinkProfileEditEntry
        {
            IdName = MakeUniqueIdName("new_profile"),
            Tier = 0,
            TransportSpeed = 0.05f,
            PackageSize = 10,
            BundleTime = 5,
            SlotCapacity = 3,
            StateOfMatter = StateOfMatter.Solid,
            IsNew = true,
            IsDirty = false,
        };
        _entries.Add(entry);
        return entry;
    }

    public void DeleteProfile(int index)
    {
        if (index < 0 || index >= _entries.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        _entries.RemoveAt(index);
    }

    public List<string> Validate()
    {
        var errors = new List<string>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < _entries.Count; i++)
        {
            var e = _entries[i];

            if (string.IsNullOrWhiteSpace(e.IdName))
                errors.Add($"Profile at index {i} has empty id_name.");
            else if (!seenIds.Add(e.IdName))
                errors.Add($"Duplicate id_name '{e.IdName}' at index {i}.");

            if (e.Tier < 0)
                errors.Add($"Profile '{e.IdName}' has negative tier.");
            if (e.TransportSpeed <= 0f)
                errors.Add($"Profile '{e.IdName}' has non-positive transport_speed.");
            if (e.PackageSize < 1)
                errors.Add($"Profile '{e.IdName}' has package_size < 1.");
            if (e.BundleTime < 0)
                errors.Add($"Profile '{e.IdName}' has negative bundle_time.");
            if (e.SlotCapacity < 1)
                errors.Add($"Profile '{e.IdName}' has slot_capacity < 1.");
            if (!Enum.IsDefined(typeof(StateOfMatter), e.StateOfMatter))
                errors.Add($"Profile '{e.IdName}' has invalid state_of_matter.");
        }

        return errors;
    }

    private string MakeUniqueIdName(string baseName)
    {
        if (!_entries.Any(e => string.Equals(e.IdName, baseName, StringComparison.Ordinal)))
            return baseName;
        int n = 2;
        while (_entries.Any(e => string.Equals(e.IdName, $"{baseName}_{n}", StringComparison.Ordinal)))
            n++;
        return $"{baseName}_{n}";
    }
}
#endif
