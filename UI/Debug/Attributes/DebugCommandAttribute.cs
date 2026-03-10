#if DEBUG
using System;

namespace UI.Debug;

/// <summary>
/// Attribute used to tag methods as console commands.
/// Supports both static and instance methods.
/// </summary>
/// <remarks>
/// Static commands are registered globally and can be called directly.
/// Instance commands require a target object via namespace prefix.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class DebugCommandAttribute : Attribute
{
    /// <summary>
    /// The command name used to invoke this command.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// A brief description of what the command does.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Usage syntax for the command (e.g., "spawn &lt;type&gt; [name]").
    /// </summary>
    public string Usage { get; }

    /// <summary>
    /// Alternative names for this command.
    /// </summary>
    public string[] Aliases { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Whether this command requires a target instance.
    /// Set to true for instance methods that operate on specific objects.
    /// </summary>
    public bool RequiresTarget { get; set; }

    /// <summary>
    /// Category for organizing commands in help output.
    /// </summary>
    public string Category { get; set; } = "General";

    /// <summary>
    /// Creates a new DebugCommandAttribute.
    /// </summary>
    /// <param name="name">The command name.</param>
    /// <param name="description">Brief description of the command.</param>
    /// <param name="usage">Usage syntax (optional).</param>
    public DebugCommandAttribute(string name, string description, string usage = "")
    {
        Name = name;
        Description = description;
        Usage = usage;
    }
}
#endif
