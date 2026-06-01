using System;
using System.Collections.Generic;
using UtilityLibrary.TaskSystem;

namespace UtilityLibrary.DataLoading
{
    /// <summary>
    /// Interface defining the contract for loadable databases that integrate with ThreadPooler.
    /// </summary>
    public interface ILoadableDatabase
    {
        /// <summary>
        /// Gets the unique name of the database.
        /// </summary>
        string DatabaseName { get; }

        /// <summary>
        /// Gets the DatabaseName values of databases that must be loaded before this one.
        /// </summary>
        IReadOnlyList<string> Dependencies => Array.Empty<string>();

        /// <summary>
        /// Gets whether the database is currently loaded.
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// Gets the current loading progress (0.0 to 1.0).
        /// </summary>
        float LoadProgress { get; }

        /// <summary>
        /// Asynchronously loads the database content.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        void LoadData();

        /// <summary>
        /// Unloads the database content, freeing resources.
        /// </summary>
        void Unload();

        /// <summary>
        /// Creates a WorkPackage for loading this database through the ThreadPooler.
        /// </summary>
        /// <returns>A configured WorkPackage for database loading.</returns>
        WorkPackage CreateLoadPackage();

        /// <summary>
        /// Event triggered when the database loading starts.
        /// </summary>
        event Action<string> OnLoadStarted;

        /// <summary>
        /// Event triggered when the database loading completes.
        /// </summary>
        event Action<string, bool> OnLoadCompleted;

        /// <summary>
        /// Event triggered when the database loading progress changes.
        /// </summary>
        event Action<string, float> OnLoadProgressChanged;
    }
}

