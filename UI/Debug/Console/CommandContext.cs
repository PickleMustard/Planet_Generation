#if DEBUG
using System;
using System.IO;
using System.Text;

namespace UI.Debug.Console;

/// <summary>
/// Context object passed to command executions containing output writer,
/// caller information, and utility methods.
/// </summary>
public class CommandContext : IDisposable
{
    private readonly StringBuilder _outputBuilder;
    private readonly StringWriter _outputWriter;

    /// <summary>
    /// The TextWriter for command output.
    /// </summary>
    public TextWriter Output => _outputWriter;

    /// <summary>
    /// The namespace of the caller (if targeting an instance).
    /// </summary>
    public string? CallerNamespace { get; }

    /// <summary>
    /// The target object instance (if command requires a target).
    /// </summary>
    public object? TargetInstance { get; }

    /// <summary>
    /// Reference to the CommandRegistry for command lookups.
    /// </summary>
    public CommandRegistry Registry { get; }

    /// <summary>
    /// Whether this context has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Creates a new CommandContext.
    /// </summary>
    /// <param name="registry">The command registry.</param>
    /// <param name="callerNamespace">The caller's namespace (optional).</param>
    /// <param name="targetInstance">The target instance (optional).</param>
    public CommandContext(CommandRegistry registry, string? callerNamespace = null, object? targetInstance = null)
    {
        Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        CallerNamespace = callerNamespace;
        TargetInstance = targetInstance;
        _outputBuilder = new StringBuilder();
        _outputWriter = new StringWriter(_outputBuilder);
    }

    /// <summary>
    /// Writes a line to the output.
    /// </summary>
    /// <param name="message">The message to write.</param>
    public void WriteLine(string message)
    {
        _outputWriter.WriteLine(message);
    }

    /// <summary>
    /// Writes to the output without a newline.
    /// </summary>
    /// <param name="message">The message to write.</param>
    public void Write(string message)
    {
        _outputWriter.Write(message);
    }

    /// <summary>
    /// Writes a formatted line to the output.
    /// </summary>
    /// <param name="format">Format string.</param>
    /// <param name="args">Format arguments.</param>
    public void WriteLine(string format, params object[] args)
    {
        _outputWriter.WriteLine(format, args);
    }

    /// <summary>
    /// Gets the accumulated output as a string.
    /// </summary>
    /// <returns>The output string.</returns>
    public string GetOutput()
    {
        _outputWriter.Flush();
        return _outputBuilder.ToString();
    }

    /// <summary>
    /// Clears the accumulated output.
    /// </summary>
    public void ClearOutput()
    {
        _outputBuilder.Clear();
    }

    /// <summary>
    /// Writes an error message with formatting.
    /// </summary>
    /// <param name="message">The error message.</param>
    public void WriteError(string message)
    {
        _outputWriter.WriteLine($"[ERROR] {message}");
    }

    /// <summary>
    /// Writes a warning message with formatting.
    /// </summary>
    /// <param name="message">The warning message.</param>
    public void WriteWarning(string message)
    {
        _outputWriter.WriteLine($"[WARNING] {message}");
    }

    /// <summary>
    /// Writes an info message with formatting.
    /// </summary>
    /// <param name="message">The info message.</param>
    public void WriteInfo(string message)
    {
        _outputWriter.WriteLine($"[INFO] {message}");
    }

    public void Dispose()
    {
        if (!IsDisposed)
        {
            _outputWriter?.Dispose();
            IsDisposed = true;
        }
    }
}
#endif
