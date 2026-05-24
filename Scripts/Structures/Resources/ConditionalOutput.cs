using System.Collections.Generic;

namespace Structures.Resources;

/// <summary>
/// A recipe output that fires only when its condition evaluates true.
/// Stacks additively with the recipe's base OutputResources.
///
/// The canonical form is <see cref="Rules"/> — a flat list of comparison
/// clauses joined by per-row AND/OR. <see cref="Condition"/> is retained only
/// as a fallback for legacy hand-written expressions that the rule builder
/// cannot represent (arithmetic, parentheses, etc.).
/// </summary>
public class ConditionalOutput
{
    public List<ConditionRule> Rules { get; set; } = new();

    /// <summary>
    /// Legacy raw expression. Populated only when loaded from the old YAML
    /// shape <c>condition: "&lt;expr&gt;"</c> AND the expression could not be
    /// decomposed into <see cref="Rules"/>. Empty otherwise.
    /// </summary>
    public string? Condition { get; set; }

    public string Resource { get; set; } = string.Empty;
    public float Amount { get; set; }

    /// <summary>
    /// Parsed AST. Populated by the recipe loader at load time so cycle-time
    /// evaluation is allocation-free. Null until compiled.
    /// </summary>
    public RecipeExpressionEvaluator.Node? CompiledCondition { get; set; }

    public bool EvaluateBool(IReadOnlyDictionary<string, float> ctx)
        => CompiledCondition != null && RecipeExpressionEvaluator.EvaluateBool(CompiledCondition, ctx);
}
