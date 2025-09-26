using Godot;
using System;
using System.IO;
using System.Text;

namespace UtilityLibrary
{
    public static class Logger
    {
        public enum Mode
        {
            DEBUG, CRITICAL, ERROR, WARNING, INFO, PROD
        }
        private static readonly string LogDirectory = "logs";
        private static readonly string LogFilePath = Path.Combine(LogDirectory, "debug.log");
        private static readonly object LockObject = new object();

        public static Mode logMode { get; set; } = Mode.PROD;


        static Logger()
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

            lock (LockObject)
            {
                try
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    string categoryTag = string.IsNullOrEmpty(category) ? "" : $"[{category}] ";
                    string levelString = level.ToString();
                    string formattedMessage = $"[{timestamp}] [{levelString.PadRight(8)}] {categoryTag}{message}";

                    // Write to file
                    File.AppendAllText(LogFilePath, formattedMessage + System.Environment.NewLine);

                    if (level == Logger.Mode.DEBUG && Logger.logMode <= Logger.Mode.DEBUG)
                        GD.Print(formattedMessage);
                    else if ((level == Logger.Mode.CRITICAL || level == Logger.Mode.ERROR || level == Logger.Mode.WARNING) && Logger.logMode <= Logger.Mode.ERROR)
                        GD.PrintErr(formattedMessage);
                    else if (level == Logger.Mode.INFO && Logger.logMode <= Logger.Mode.INFO)
                        GD.Print(formattedMessage);
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
