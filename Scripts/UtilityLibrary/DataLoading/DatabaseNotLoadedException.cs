using System;

namespace UtilityLibrary.DataLoading
{
    /// <summary>
    /// Exception thrown when attempting to access a database that has not been loaded.
    /// </summary>
    public class DatabaseNotLoadedException : Exception
    {
        /// <summary>
        /// Gets the name of the database that was not loaded.
        /// </summary>
        public string DatabaseName { get; }

        /// <summary>
        /// Initializes a new instance of the DatabaseNotLoadedException class.
        /// </summary>
        /// <param name="databaseName">The name of the database that was not loaded.</param>
        public DatabaseNotLoadedException(string databaseName)
            : base($"Database '{databaseName}' has not been loaded. Call LoadAsync() or ensure it's registered with DatabaseLoadManager.")
        {
            DatabaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
        }

        /// <summary>
        /// Initializes a new instance of the DatabaseNotLoadedException class.
        /// </summary>
        /// <param name="databaseName">The name of the database that was not loaded.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public DatabaseNotLoadedException(string databaseName, Exception innerException)
            : base($"Database '{databaseName}' has not been loaded. Call LoadAsync() or ensure it's registered with DatabaseLoadManager.", innerException)
        {
            DatabaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
        }
    }

    /// <summary>
    /// Exception thrown when a database fails to load.
    /// </summary>
    public class DatabaseLoadFailedException : Exception
    {
        /// <summary>
        /// Gets the name of the database that failed to load.
        /// </summary>
        public string DatabaseName { get; }

        /// <summary>
        /// Initializes a new instance of the DatabaseLoadFailedException class.
        /// </summary>
        /// <param name="databaseName">The name of the database that failed to load.</param>
        /// <param name="errorMessage">The error message describing the failure.</param>
        public DatabaseLoadFailedException(string databaseName, string errorMessage)
            : base($"Failed to load database '{databaseName}': {errorMessage}")
        {
            DatabaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
        }

        /// <summary>
        /// Initializes a new instance of the DatabaseLoadFailedException class.
        /// </summary>
        /// <param name="databaseName">The name of the database that failed to load.</param>
        /// <param name="errorMessage">The error message describing the failure.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public DatabaseLoadFailedException(string databaseName, string errorMessage, Exception innerException)
            : base($"Failed to load database '{databaseName}': {errorMessage}", innerException)
        {
            DatabaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
        }
    }

    /// <summary>
    /// Exception thrown when a database is not registered with the DatabaseLoadManager.
    /// </summary>
    public class DatabaseNotRegisteredException : Exception
    {
        /// <summary>
        /// Gets the name of the database that is not registered.
        /// </summary>
        public string DatabaseName { get; }

        /// <summary>
        /// Initializes a new instance of the DatabaseNotRegisteredException class.
        /// </summary>
        /// <param name="databaseName">The name of the database that is not registered.</param>
        public DatabaseNotRegisteredException(string databaseName)
            : base($"Database '{databaseName}' is not registered with DatabaseLoadManager. Call RegisterDatabase() first.")
        {
            DatabaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
        }

        /// <summary>
        /// Initializes a new instance of the DatabaseNotRegisteredException class.
        /// </summary>
        /// <param name="databaseName">The name of the database that is not registered.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public DatabaseNotRegisteredException(string databaseName, Exception innerException)
            : base($"Database '{databaseName}' is not registered with DatabaseLoadManager. Call RegisterDatabase() first.", innerException)
        {
            DatabaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
        }
    }
}