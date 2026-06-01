#if DEBUG
using System.Collections.Generic;

namespace Debug.Console;

/// <summary>
/// Interface that all console commands must implement.
/// Defines execution and autocomplete suggestion methods.
/// </summary>
public interface ICommand
{
    /// <summary>
    /// Executes the command with the given context and arguments.
    /// </summary>
    /// <param name="context">The execution context containing output and utilities.</param>
    /// <param name="args">The arguments passed to the command.</param>
    /// <returns>Exit code (0 for success, non-zero for error).</returns>
    int Execute(CommandContext context, string[] args);

    /// <summary>
    /// Gets autocomplete suggestions for the current argument position.
    /// </summary>
    /// <param name="args">The current arguments typed so far.</param>
    /// <returns>A list of possible completions.</returns>
    IEnumerable<string> GetSuggestions(string[] args);
}
#endif
