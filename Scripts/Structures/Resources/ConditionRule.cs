namespace Structures.Resources;

public enum ConditionOperator
{
    Eq,
    NotEq,
    Lt,
    Lte,
    Gt,
    Gte,
}

public enum ConditionJoin
{
    And,
    Or,
}

/// <summary>
/// A single comparison clause inside a recipe's conditional output. Rules are
/// composed flat (no nesting) via <see cref="Join"/>; join is ignored on the
/// first rule of a list.
/// </summary>
public sealed class ConditionRule
{
    public ConditionJoin Join { get; set; } = ConditionJoin.And;
    public string Variable { get; set; } = string.Empty;
    public ConditionOperator Operator { get; set; } = ConditionOperator.Eq;
    public float Value { get; set; }
}

public static class ConditionOperatorExtensions
{
    public static string ToSymbol(this ConditionOperator op) => op switch
    {
        ConditionOperator.Eq => "==",
        ConditionOperator.NotEq => "!=",
        ConditionOperator.Lt => "<",
        ConditionOperator.Lte => "<=",
        ConditionOperator.Gt => ">",
        ConditionOperator.Gte => ">=",
        _ => "==",
    };

    public static bool TryParseSymbol(string symbol, out ConditionOperator op)
    {
        switch (symbol)
        {
            case "==": op = ConditionOperator.Eq; return true;
            case "!=": op = ConditionOperator.NotEq; return true;
            case "<":  op = ConditionOperator.Lt;   return true;
            case "<=": op = ConditionOperator.Lte;  return true;
            case ">":  op = ConditionOperator.Gt;   return true;
            case ">=": op = ConditionOperator.Gte;  return true;
            default:   op = ConditionOperator.Eq;   return false;
        }
    }
}

public static class ConditionJoinExtensions
{
    public static string ToYamlString(this ConditionJoin join) =>
        join == ConditionJoin.Or ? "OR" : "AND";

    public static bool TryParseYaml(string text, out ConditionJoin join)
    {
        if (string.Equals(text, "OR", System.StringComparison.OrdinalIgnoreCase))
        {
            join = ConditionJoin.Or;
            return true;
        }
        if (string.Equals(text, "AND", System.StringComparison.OrdinalIgnoreCase))
        {
            join = ConditionJoin.And;
            return true;
        }
        join = ConditionJoin.And;
        return false;
    }
}
