#if DEBUG
using System.Collections.Generic;
using System.Linq;
using Structures.Resources;

namespace DeveloperTools.Common;

/// <summary>Shared read-only lookups for developer editors (resource ids, etc.).</summary>
public static class EditorLookups
{
    /// <summary>All resource ids from the loaded ResourceDatabase, sorted. Empty on failure.</summary>
    public static List<string> GetAllResourceIds()
    {
        try
        {
            var db = ResourceDatabase.Instance;
            if (db != null && db.IsLoaded)
                return db.GetAllResources().Keys.OrderBy(s => s).ToList();
        }
        catch { }
        return new List<string>();
    }
}
#endif
