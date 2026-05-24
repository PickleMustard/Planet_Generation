#if DEBUG
using System;
using Structures.Resources;
using UtilityLibrary;
using UtilityLibrary.DataLoading;

namespace DeveloperTools.Common;

/// <summary>
/// Reloads runtime singleton databases after a developer editor saves YAML to disk,
/// so the running game reflects edits without an engine restart. Emits
/// <see cref="SignalBus.DatabaseReloaded"/> on success so UI consumers can refresh.
/// </summary>
public static class EditorDatabaseReloader
{
    public static bool ReloadByName(string databaseName)
    {
        if (string.IsNullOrEmpty(databaseName))
        {
            GameLogger.Error("EditorDatabaseReloader: empty database name");
            return false;
        }

        ILoadableDatabase? db =
            DatabaseLoadManager.Instance?.GetDatabase(databaseName)
            ?? ResolveUnregistered(databaseName);

        if (db == null)
        {
            GameLogger.Error($"EditorDatabaseReloader: '{databaseName}' not found");
            return false;
        }

        try
        {
            db.LoadData();
            SignalBus.Instance?.EmitDatabaseReloaded(databaseName);
            GameLogger.Info($"EditorDatabaseReloader: reloaded '{databaseName}'");
            return true;
        }
        catch (Exception ex)
        {
            GameLogger.Error($"EditorDatabaseReloader: '{databaseName}' reload failed: {ex.Message}");
            return false;
        }
    }

    public static int ReloadAll(params string[] databaseNames)
    {
        int ok = 0;
        foreach (var name in databaseNames)
        {
            if (ReloadByName(name)) ok++;
        }
        return ok;
    }

    private static ILoadableDatabase? ResolveUnregistered(string databaseName) =>
        databaseName switch
        {
            "BuildingShape2DDatabase" => BuildingShape2DDatabase.Instance,
            "LinkProfileDatabase" => LinkProfileDatabase.Instance,
            _ => null,
        };
}
#endif
