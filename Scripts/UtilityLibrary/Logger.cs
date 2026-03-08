using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UtilityLibrary
{
    public static class GameLogger
    {
        public enum Mode
        {
            DEBUG, CRITICAL, ERROR, WARNING, INFO, PROD
        }
        private static readonly string LogDirectory = "logs";
        private static readonly string LogFilePath = Path.Combine(LogDirectory, "debug.log");
        private static readonly object LockObject = new object();

        public static Mode logMode { get; set; } = Mode.DEBUG;
        private static bool _logToFile = true;
        private static bool _logToConsole = true;

        private static readonly ConfigurableProvider Provider = new ConfigurableProvider();

        private class ConfigurableProvider : IConfigurable
        {
            public string SettingsCategory => "logging";

            public IEnumerable<ConfigEntry> GetConfigEntries() => new[]
            {
                new ConfigEntry
                {
                    Key = "level",
                    ValueType = typeof(string),
                    DefaultValue = "DEBUG",
                    ValidOptions = new[] { "DEBUG", "INFO", "WARNING", "ERROR", "CRITICAL", "PROD" },
                    Description = "Minimum log level to output",
                    RequiresRestart = false
                },
                new ConfigEntry
                {
                    Key = "log_to_file",
                    ValueType = typeof(bool),
                    DefaultValue = true,
                    Description = "Write log messages to logs/debug.log",
                    RequiresRestart = false
                },
                new ConfigEntry
                {
                    Key = "log_to_console",
                    ValueType = typeof(bool),
                    DefaultValue = true,
                    Description = "Print log messages to Godot console",
                    RequiresRestart = false
                }
            };

            public void ApplySetting(string key, object value)
            {
                switch (key)
                {
                    case "level":
                        var levelStr = value.ToString();
                        if (Enum.TryParse<Mode>(levelStr, out var modeEnum))
                        {
                            logMode = modeEnum;
                        }
                        break;
                    case "log_to_file":
                        _logToFile = (bool)value;
                        break;
                    case "log_to_console":
                        _logToConsole = (bool)value;
                        break;
                    default:
                        break;
                }
            }

            public object GetSettingDefault(string key) => key switch
            {
                "level" => "DEBUG",
                "log_to_file" => true,
                "log_to_console" => true,
                _ => null
            };
        }

        static GameLogger()
        {
            // Ensure log directory exists
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }

            // Clear previous log file
            if (File.Exists(LogFilePath))
            {
                File.Delete(LogFilePath);
            }

            File.Create(LogFilePath).Close();
        }

        /// <summary>
        /// Initializes the GameLogger by registering with RuntimeSettings.
        /// Called lazily when RuntimeSettings is available.
        /// </summary>
        public static void Initialize()
        {
            RuntimeSettings.Instance?.RegisterConfigurable(Provider);
        }

        private static Mode ConvertStringToMode(String input)
        {
            return (Mode)Enum.Parse(typeof(Mode), input);
        }

        /// <summary>
        /// Logs a debug message with timestamp
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="category">Category of the log message (optional)</param>
        public static void Debug(string message, string category = "")
        {
            LogMessage(message, ConvertStringToMode("DEBUG"), category);
        }

        /// <summary>
        /// Logs an info message with timestamp
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="category">Category of the log message (optional)</param>
        public static void Info(string message, string category = "")
        {
            LogMessage(message, ConvertStringToMode("INFO"), category);
        }

        /// <summary>
        /// Logs a warning message with timestamp
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="category">Category of the log message (optional)</param>
        public static void Warning(string message, string category = "")
        {
            LogMessage(message, ConvertStringToMode("WARNING"), category);
        }

        /// <summary>
        /// Logs an error message with timestamp
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="category">Category of the log message (optional)</param>
        public static void Error(string message, string category = "")
        {
            LogMessage(message, ConvertStringToMode("ERROR"), category);
        }

        /// <summary>
        /// Logs a critical error message with timestamp
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="category">Category of the log message (optional)</param>
        public static void Critical(string message, string category = "")
        {
            LogMessage(message, ConvertStringToMode("CRITICAL"), category);
        }

        /// <summary>
        /// Logs a function entry point
        /// </summary>
        /// <param name="functionName">Name of the function</param>
        /// <param name="parameters">Function parameters (optional)</param>
        public static void EnterFunction(string functionName, string parameters = "")
        {
            string message = string.IsNullOrEmpty(parameters) ?
                $"Entering function: {functionName}" :
                $"Entering function: {functionName}({parameters})";
            LogMessage(message, ConvertStringToMode("DEBUG"), "ENTER FUNCTION");
        }

        /// <summary>
        /// Logs a function exit point
        /// </summary>
        /// <param name="functionName">Name of the function</param>
        /// <param name="returnValue">Return value (optional)</param>
        public static void ExitFunction(string functionName, string returnValue = "")
        {
            string message = string.IsNullOrEmpty(returnValue) ?
                $"Exiting function: {functionName}" :
                $"Exiting function: {functionName} => {returnValue}";
            LogMessage(message, ConvertStringToMode("DEBUG"), "EXIT FUNCTION");
        }

        /// <summary>
        /// Logs triangulation-specific data
        /// </summary>
        /// <param name="message">Triangulation message</param>
        public static void Triangulation(string message)
        {
            LogMessage(message, ConvertStringToMode("INFO"), "TRIANGULATING MESH");
        }

        /// <summary>
        /// Logs edge-related information
        /// </summary>
        /// <param name="message">Edge message</param>
        public static void Edge(string message)
        {
            LogMessage(message, ConvertStringToMode("INFO"), "EDGE MESH");
        }

        /// <summary>
        /// Logs point-related information
        /// </summary>
        /// <param name="message">Point message</param>
        public static void Point(string message)
        {
            LogMessage(message, ConvertStringToMode("INFO"), "POINT MESH");
        }

        /// <summary>
        /// Logs triangle-related information
        /// </summary>
        /// <param name="message">Triangle message</param>
        public static void Triangle(string message)
        {
            LogMessage(message, ConvertStringToMode("INFO"), "TRIANGLE MESH");
        }

        /// <summary>
        /// Internal method to write formatted log messages to file and console
        /// </summary>
        /// <param name="message">Message to log</param>
        /// <param name="level">Log level</param>
        /// <param name="category">Category of the message</param>
        private static void LogMessage(string message, Mode level, string category)
        {
            // Don't log timer animation messages
            if (message.Contains("Processing:") && (message.Contains("⠋") || message.Contains("⠙") || message.Contains("⠹") ||
                message.Contains("⠸") || message.Contains("⠼") || message.Contains("⠴") ||
                message.Contains("⠦") || message.Contains("⠧") || message.Contains("⠇") || message.Contains("⠏")))
            {
                return;
            }

            // Use internal cached values as defaults to avoid querying RuntimeSettings
            // on every log call (which would cause recursive issues during initialization)
            bool logToFile = _logToFile;
            bool logToConsole = _logToConsole;

            // Only query RuntimeSettings if it's available and has the settings registered
            if (RuntimeSettings.Instance != null)
            {
                // Use HasSetting to check without triggering "Setting not found" errors
                if (RuntimeSettings.Instance.HasSetting("logging", "log_to_file"))
                {
                    logToFile = RuntimeSettings.Instance.GetSetting<bool>("logging", "log_to_file");
                }
                if (RuntimeSettings.Instance.HasSetting("logging", "log_to_console"))
                {
                    logToConsole = RuntimeSettings.Instance.GetSetting<bool>("logging", "log_to_console");
                }
            }

            lock (LockObject)
            {
                try
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string categoryTag = string.IsNullOrEmpty(category) ? "" : $"[{category}] ";
                    string levelString = level.ToString();
                    string formattedMessage = $"[{timestamp}] [{levelString.PadRight(8)}] {categoryTag}{message}";

                    // Write to file
                    if (logToFile)
                    {
                        File.AppendAllText(LogFilePath, formattedMessage + System.Environment.NewLine);
                    }

                    if (logToConsole)
                    {
                        if (level == Mode.DEBUG && logMode <= Mode.DEBUG)
                            GD.Print(formattedMessage);
                        else if ((level == Mode.CRITICAL || level == Mode.ERROR || level == Mode.WARNING) && logMode <= Mode.ERROR)
                            GD.PrintErr(formattedMessage);
                        else if (level == Mode.INFO && logMode <= Mode.INFO)
                            GD.Print(formattedMessage);
                    }
                }
                catch (Exception ex)
                {
                    // If we can't log to file, at least print to console
                    GD.PrintErr($"Failed to write to log file: {ex.Message}");
                    GD.Print($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level.ToString().PadRight(8)}] {message}");
                }
            }
        }

        /// <summary>
        /// Gets the path to the current log file
        /// </summary>
        /// <returns>Path to log file</returns>
        public static string GetLogFilePath()
        {
            return LogFilePath;
        }
    }
}
