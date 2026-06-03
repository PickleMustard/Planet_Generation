#if TOOLS
using Godot;
#endif

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProceduralGeneration.SubtypeSystem;
using Structures.Resources;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using SysDict = System.Collections.Generic.Dictionary<object, object>;
using SysList = System.Collections.Generic.List<object>;

namespace UtilityLibrary.Tools;

/// <summary>
/// Phase 6 one-shot migrator: rewrites <c>Configuration/SystemTemplate/*.yaml</c> from the
/// pre-Phase-6 fat shape (inline <c>base_mesh</c> / <c>tectonics</c> /
/// <c>spherical_harmonics_settings</c>) into the slim shape (subtype slots + optional weights).
///
/// Per-body gen ranges now live in <c>Configuration/Subtypes/*.yaml</c> and are rolled by
/// <see cref="SubtypeGenParamResolver"/> at generation time. Legacy keys still present in
/// SystemTemplate yaml are stripped; bodies that lack both <c>subtype</c> and
/// <c>subtype_weights</c> receive a uniform distribution over the family's registered subtypes.
/// </summary>
public static class SystemTemplateMigrator
{
    public const string DefaultDir = "res://Configuration/SystemTemplate";

    private static readonly string[] LegacyKeys =
    {
        "base_mesh",
        "tectonics",
        "spherical_harmonics_settings",
    };

    public sealed class FileReport
    {
        public string Path = string.Empty;
        public int BodiesScanned;
        public int LegacyBlocksStripped;
        public int SubtypeSlotsFilled;
        public List<string> Warnings = new();
        public bool Changed;
        public string? Error;
    }

    public sealed class Report
    {
        public List<FileReport> Files = new();
        public int TotalChanged => Files.Count(f => f.Changed);

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"SystemTemplateMigrator report — {Files.Count} file(s), {TotalChanged} changed");
            foreach (var fr in Files)
            {
                string status = fr.Error != null
                    ? "ERROR"
                    : fr.Changed
                        ? "CHANGED"
                        : "ok";
                sb.AppendLine(
                    $"  [{status}] {fr.Path} — scanned {fr.BodiesScanned}, stripped {fr.LegacyBlocksStripped}, filled {fr.SubtypeSlotsFilled}"
                );
                if (fr.Error != null) sb.AppendLine($"    ! {fr.Error}");
                foreach (var w in fr.Warnings) sb.AppendLine($"    - {w}");
            }
            return sb.ToString();
        }
    }

    /// <summary>Migrate every <c>*.yaml</c> in <see cref="DefaultDir"/>. Writes files in place.</summary>
    public static Report MigrateAll(string dirPath = DefaultDir, bool dryRun = false)
    {
        var report = new Report();
        if (!Godot.DirAccess.DirExistsAbsolute(dirPath))
        {
            GameLogger.Error($"SystemTemplateMigrator: directory not found {dirPath}");
            return report;
        }

        EnsureSubtypeDatabase();

        var files = Godot.DirAccess.GetFilesAt(dirPath);
        var yamlNames = files.Where(f => f.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToList();
        var tomlNames = files.Where(f => f.EndsWith(".toml", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var name in yamlNames)
        {
            string path = $"{dirPath}/{name}";
            var fr = MigrateFile(path, dryRun);

            // Plan: warn if a .toml sibling of the same base name exists.
            string tomlSibling = System.IO.Path.GetFileNameWithoutExtension(name) + ".toml";
            if (tomlNames.Contains(tomlSibling))
            {
                fr.Warnings.Add($"TOML sibling '{tomlSibling}' exists — deprecated, prefer yaml");
            }
            report.Files.Add(fr);
        }

        GameLogger.Info(report.ToString());
        return report;
    }

    /// <summary>Migrate a single file. Returns its report; on hard error, <c>fr.Error</c> is set.</summary>
    public static FileReport MigrateFile(string path, bool dryRun = false)
    {
        var fr = new FileReport { Path = path };

        if (!Godot.FileAccess.FileExists(path))
        {
            fr.Error = "file does not exist";
            return fr;
        }

        try
        {
            using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
            string text = f.GetAsText();

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            // YamlDotNet returns nested Dictionary<object,object> / List<object>; we preserve
            // unknown keys verbatim and only mutate the ones the migrator cares about.
            var raw = deserializer.Deserialize<SysDict>(text);
            if (raw == null)
            {
                fr.Error = "deserialized to null (empty file?)";
                return fr;
            }

            foreach (var section in new[] { "dominant", "belts", "planetary" })
            {
                if (!raw.TryGetValue(section, out var sectionNode)) continue;
                if (sectionNode is not SysList bodies) continue;

                foreach (var entry in bodies)
                {
                    if (entry is not SysDict body) continue;
                    MigrateBody(body, fr, isBelt: section == "belts");
                }
            }

            if (!fr.Changed)
            {
                return fr;
            }

            // Serialise. UnderscoredNamingConvention preserves the snake_case keys we want.
            var serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();
            string newText = serializer.Serialize(raw);

            if (!dryRun)
            {
                using var w = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
                if (w == null)
                {
                    fr.Error = $"could not open for write (FileAccess.Open returned null)";
                    return fr;
                }
                w.StoreString(newText);
            }
        }
        catch (Exception e)
        {
            fr.Error = $"exception: {e.Message}";
        }

        return fr;
    }

    private static void MigrateBody(SysDict body, FileReport fr, bool isBelt)
    {
        fr.BodiesScanned++;

        // 1. Strip legacy gen-range blocks.
        foreach (var key in LegacyKeys)
        {
            if (body.Remove(key))
            {
                fr.LegacyBlocksStripped++;
                fr.Changed = true;
            }
        }

        // 2. Fill subtype slot if absent.
        bool hasExplicit = body.ContainsKey("subtype")
            && body["subtype"] is string s && !string.IsNullOrEmpty(s);
        bool hasWeights = body.ContainsKey("subtype_weights")
            && body["subtype_weights"] is SysDict d && d.Count > 0;

        if (!hasExplicit && !hasWeights)
        {
            if (TryResolveFamily(body, out var family))
            {
                var subtypeIds = SubtypeIdsForFamily(family);
                if (subtypeIds.Count == 0)
                {
                    fr.Warnings.Add(
                        $"no subtypes registered for family {family} — body left without subtype slot"
                    );
                }
                else
                {
                    float weight = (float)Math.Round(1.0 / subtypeIds.Count, 4);
                    var weightsOut = new SysDict();
                    foreach (var id in subtypeIds)
                        weightsOut[id] = weight;
                    body["subtype_weights"] = weightsOut;
                    fr.SubtypeSlotsFilled++;
                    fr.Changed = true;
                }
            }
            else
            {
                fr.Warnings.Add(
                    $"body has no resolvable 'type' field — cannot infer family for subtype_weights"
                );
            }
        }

        // 3. Recurse into satellites: same strip pass (no subtype fill — satellites have their
        //    own family slot that requires special handling; warn instead).
        if (body.TryGetValue("satellites", out var satsNode) && satsNode is SysList sats)
        {
            foreach (var satEntry in sats)
            {
                if (satEntry is not SysDict sat) continue;
                MigrateBody(sat, fr, isBelt: false);
            }
        }
    }

    private static bool TryResolveFamily(SysDict body, out BodyFamily family)
    {
        family = default;
        if (!body.TryGetValue("type", out var typeObj) || typeObj is not string typeStr)
            return false;

        // OrbitalBodyType / OrbitalBodyType / SatelliteGroupTypes all overlap by name in YAML.
        // We map by string match to BodyFamily so the migrator does not depend on which enum the
        // template authored.
        return typeStr switch
        {
            "Star" => Set(out family, BodyFamily.Star),
            "BlackHole" => Set(out family, BodyFamily.BlackHole),
            "NeutronStar" => Set(out family, BodyFamily.NeutronStar),
            "RockyPlanet" => Set(out family, BodyFamily.RockyPlanet),
            "GasGiant" => Set(out family, BodyFamily.GasGiant),
            "IceGiant" => Set(out family, BodyFamily.IceGiant),
            "DwarfPlanet" => Set(out family, BodyFamily.DwarfPlanet),
            "Moon" or "Satellite" or "Asteroid" or "Comet" or "Rings" => Set(out family, BodyFamily.Satellite),
            "AsteroidBelt" or "IceBelt" => Set(out family, BodyFamily.Belt),
            _ => false,
        };

        static bool Set(out BodyFamily f, BodyFamily value) { f = value; return true; }
    }

    private static List<string> SubtypeIdsForFamily(BodyFamily family)
    {
        try
        {
            return SubtypeDatabase.Instance.ByFamily(family)
                .Select(s => s.Id)
                .Where(id => !string.IsNullOrEmpty(id))
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception e)
        {
            GameLogger.Warning($"SystemTemplateMigrator: ByFamily({family}) failed: {e.Message}");
            return new List<string>();
        }
    }

    private static void EnsureSubtypeDatabase()
    {
        if (!SubtypeDatabase.Instance.IsLoaded)
        {
            try { SubtypeDatabase.Instance.LoadData(); }
            catch (Exception e)
            {
                GameLogger.Error($"SystemTemplateMigrator: SubtypeDatabase load failed: {e.Message}");
            }
        }
    }
}
