using System;
using Godot;
using UtilityLibrary.SaveLoad.Dto;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using FileAccess = Godot.FileAccess;

namespace UtilityLibrary.SaveLoad;

/// <summary>
/// YAML (de)serialization for save files using the strongly-typed POCO path (NOT the Variant path
/// used by TemplateLoader). Reads/writes through Godot FileAccess so the user:// virtual filesystem
/// is honoured. Mirrors the write pattern in SubtypeYamlIO and the read pattern in TemplateLoader.
/// </summary>
public static class SaveYaml
{
    public const string SaveDir = "user://saves";
    public const int CurrentVersion = 8;

    private static readonly ISerializer _serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    private static readonly IDeserializer _deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static string Serialize(SaveFileDto dto) => _serializer.Serialize(dto);

    public static SaveFileDto Deserialize(string yaml) => _deserializer.Deserialize<SaveFileDto>(yaml);

    /// <summary>Ensures the user:// saves directory exists. Returns false on failure.</summary>
    public static bool EnsureSaveDir()
    {
        var err = DirAccess.MakeDirRecursiveAbsolute(SaveDir);
        if (err != Error.Ok && err != Error.AlreadyExists)
        {
            GameLogger.Error($"[SaveYaml] Failed to create save dir '{SaveDir}': {err}");
            return false;
        }
        return true;
    }

    /// <summary>Writes serialized YAML to the given user:// (or absolute) path. Returns false on failure.</summary>
    public static bool WriteToFile(string path, SaveFileDto dto)
    {
        if (!EnsureSaveDir())
            return false;

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GameLogger.Error($"[SaveYaml] Could not open '{path}' for write: {FileAccess.GetOpenError()}");
            return false;
        }

        try
        {
            file.StoreString(Serialize(dto));
            return true;
        }
        catch (Exception e)
        {
            GameLogger.Error($"[SaveYaml] Serialize/write failed for '{path}': {e.Message}");
            return false;
        }
    }

    /// <summary>Reads and deserializes a save file. Returns null on failure.</summary>
    public static SaveFileDto? ReadFromFile(string path)
    {
        if (!FileAccess.FileExists(path))
        {
            GameLogger.Error($"[SaveYaml] Save file not found: '{path}'");
            return null;
        }

        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GameLogger.Error($"[SaveYaml] Could not open '{path}' for read: {FileAccess.GetOpenError()}");
            return null;
        }

        try
        {
            return Deserialize(file.GetAsText());
        }
        catch (Exception e)
        {
            GameLogger.Error($"[SaveYaml] Deserialize failed for '{path}': {e.Message}");
            return null;
        }
    }
}
