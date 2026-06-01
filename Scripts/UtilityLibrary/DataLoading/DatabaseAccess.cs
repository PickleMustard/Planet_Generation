using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace UtilityLibrary.DataLoading
{
    /// <summary>
    /// Static helper class for safe database access with explicit initialization enforcement.
    /// </summary>
    public static class DatabaseAccess
    {
        /// <summary>
        /// Ensures that a database is loaded before accessing it.
        /// </summary>
        /// <typeparam name="TDatabase">The type of database to access.</typeparam>
        /// <param name="databaseName">The name of the database.</param>
        /// <param name="database">The database instance (if loaded).</param>
        /// <returns>True if the database is loaded and accessible, false otherwise.</returns>
        public static bool EnsureDatabaseLoaded<TDatabase>(string databaseName, out TDatabase? database)
            where TDatabase : class, ILoadableDatabase
        {
            database = null;

            if (string.IsNullOrEmpty(databaseName))
            {
                GD.PrintErr("DatabaseAccess: Database name cannot be null or empty");
                return false;
            }

            var loadManager = DatabaseLoadManager.Instance;
            if (loadManager == null)
            {
                GD.PrintErr("DatabaseAccess: DatabaseLoadManager is not initialized");
                return false;
            }

            var db = loadManager.GetDatabase(databaseName) as TDatabase;
            if (db == null)
            {
                GD.PrintErr($"DatabaseAccess: Database '{databaseName}' is not registered or is of incorrect type");
                return false;
            }

            if (!db.IsLoaded)
            {
                throw new DatabaseNotLoadedException(databaseName);
            }

            database = db;
            return true;
        }

        /// <summary>
        /// Gets a database with type safety, throwing an exception if not loaded.
        /// </summary>
        /// <typeparam name="TDatabase">The type of database to retrieve.</typeparam>
        /// <param name="databaseName">The name of the database.</param>
        /// <returns>The loaded database instance.</returns>
        /// <exception cref="DatabaseNotLoadedException">Thrown if the database is not loaded.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the database is not registered or of incorrect type.</exception>
        public static TDatabase GetDatabase<TDatabase>(string databaseName)
            where TDatabase : class, ILoadableDatabase
        {
            if (!EnsureDatabaseLoaded(databaseName, out TDatabase? database) || database == null)
            {
                var loadManager = DatabaseLoadManager.Instance;
                if (loadManager == null)
                {
                    throw new InvalidOperationException("DatabaseLoadManager is not initialized");
                }

                if (!loadManager.IsDatabaseRegistered(databaseName))
                {
                    throw new InvalidOperationException($"Database '{databaseName}' is not registered");
                }

                throw new DatabaseNotLoadedException(databaseName);
            }

            return database;
        }

        /// <summary>
        /// Gets a database with type safety, returning null if not loaded.
        /// </summary>
        /// <typeparam name="TDatabase">The type of database to retrieve.</typeparam>
        /// <param name="databaseName">The name of the database.</param>
        /// <returns>The loaded database instance, or null if not loaded.</returns>
        public static TDatabase? TryGetDatabase<TDatabase>(string databaseName)
            where TDatabase : class, ILoadableDatabase
        {
            EnsureDatabaseLoaded(databaseName, out TDatabase? database);
            return database;
        }

        /// <summary>
        /// Checks if a database is loaded and ready for access.
        /// </summary>
        /// <param name="databaseName">The name of the database.</param>
        /// <returns>True if the database is loaded and accessible, false otherwise.</returns>
        public static bool IsDatabaseLoaded(string databaseName)
        {
            if (string.IsNullOrEmpty(databaseName))
                return false;

            var loadManager = DatabaseLoadManager.Instance;
            if (loadManager == null)
                return false;

            return loadManager.GetDatabaseLoaded(databaseName);
        }

        /// <summary>
        /// Gets the loading progress of a database.
        /// </summary>
        /// <param name="databaseName">The name of the database.</param>
        /// <returns>The current loading progress (0.0 to 1.0), or -1 if the database is not registered.</returns>
        public static float GetDatabaseProgress(string databaseName)
        {
            if (string.IsNullOrEmpty(databaseName))
                return -1f;

            var loadManager = DatabaseLoadManager.Instance;
            if (loadManager == null)
                return -1f;

            return loadManager.GetDatabaseProgress(databaseName);
        }

        /// <summary>
        /// Waits for a database to be loaded asynchronously.
        /// </summary>
        /// <param name="databaseName">The name of the database.</param>
        /// <param name="timeoutMs">Maximum time to wait in milliseconds (default: 30000).</param>
        /// <returns>True if the database was loaded within the timeout, false otherwise.</returns>
        public static async Task<bool> WaitForDatabaseLoadedAsync(string databaseName, int timeoutMs = 30000)
        {
            if (string.IsNullOrEmpty(databaseName))
                return false;

            var loadManager = DatabaseLoadManager.Instance;
            if (loadManager == null)
                return false;

            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromMilliseconds(timeoutMs);

            while (DateTime.Now - startTime < timeout)
            {
                if (loadManager.GetDatabaseLoaded(databaseName))
                    return true;

                await Task.Delay(100);
            }

            GD.PrintErr($"DatabaseAccess: Timeout waiting for database '{databaseName}' to load");
            return false;
        }

        /// <summary>
        /// Gets all registered database names.
        /// </summary>
        /// <returns>A list of registered database names, or empty list if DatabaseLoadManager is not initialized.</returns>
        public static IEnumerable<string> GetRegisteredDatabaseNames()
        {
            var loadManager = DatabaseLoadManager.Instance;
            if (loadManager == null)
                return Array.Empty<string>();

            return loadManager.GetRegisteredDatabaseNames();
        }

        /// <summary>
        /// Validates that all required databases are loaded.
        /// </summary>
        /// <param name="databaseNames">The names of databases to validate.</param>
        /// <returns>True if all databases are loaded, false otherwise.</returns>
        public static bool ValidateAllDatabasesLoaded(params string[] databaseNames)
        {
            foreach (var dbName in databaseNames)
            {
                if (!IsDatabaseLoaded(dbName))
                {
                    GD.PrintErr($"DatabaseAccess: Database '{dbName}' is not loaded");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Ensures that at least one of the specified databases is loaded.
        /// </summary>
        /// <param name="databaseNames">The names of databases to check.</param>
        /// <returns>True if at least one database is loaded, false otherwise.</returns>
        public static bool ValidateAnyDatabaseLoaded(params string[] databaseNames)
        {
            foreach (var dbName in databaseNames)
            {
                if (IsDatabaseLoaded(dbName))
                    return true;
            }
            return false;
        }
    }
}