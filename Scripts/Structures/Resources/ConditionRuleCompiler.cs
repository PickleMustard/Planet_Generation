using System.Collections.Generic;
using static Structures.Resources.RecipeExpressionEvaluator;

namespace Structures.Resources;

/// <summary>
/// Bridge between flat <see cref="ConditionRule"/> lists (the editor's
/// canonical form) and the <see cref="RecipeExpressionEvaluator.Node"/> AST
/// (the runtime's evaluation form).
///
/// Composition is strictly left-to-right — there is no operator precedence
/// between <c>AND</c> and <c>OR</c> because the rule-builder UI is flat. Any
/// caller that wants C-style <c>&amp;&amp;</c>-before-<c>||</c> must write
/// the expression as a raw string and let <see cref="RecipeExpressionEvaluator.Compile"/>
/// handle it.
/// </summary>
public static class ConditionRuleCompiler
{
    public static Node BuildAst(IReadOnlyList<ConditionRule> rules)
    {
        if (rules == null || rules.Count == 0)
            throw new RecipeExpressionException("Cannot build AST from empty rule list.");

        Node node = MakeComparison(rules[0]);
        for (int i = 1; i < rules.Count; i++)
        {
            var rhs = MakeComparison(rules[i]);
            var joinTok = rules[i].Join == ConditionJoin.Or ? TokenKind.Or : TokenKind.And;
            node = new BinaryNode(joinTok, node, rhs);
        }
        return node;
    }

    public static bool TryDecompose(Node ast, out List<ConditionRule> rules)
    {
        rules = new List<ConditionRule>();
        if (ast == null) return false;

        // Only a strictly left-leaning chain of AND/OR joins over comparison
        // leaves can round-trip safely. A right-leaning subtree (e.g. from
        // `a || b && c`, which parses as `a || (b && c)`) has different
        // semantics than the flat left-to-right form the rule UI produces.
        var collected = new List<ConditionRule>();
        if (!Walk(ast, ConditionJoin.And, collected))
            return false;
        if (collected.Count == 0) return false;
        rules = collected;
        return true;
    }

    private static bool Walk(Node node, ConditionJoin joinForFirstLeaf, List<ConditionRule> output)
    {
        if (node is BinaryNode bin && (bin.Op == TokenKind.And || bin.Op == TokenKind.Or))
        {
            // Refuse right-leaning: rhs must be a leaf comparison so the flat
            // rule list preserves the original AST's evaluation order.
            if (bin.Rhs is BinaryNode rhsBin && (rhsBin.Op == TokenKind.And || rhsBin.Op == TokenKind.Or))
                return false;

            if (!Walk(bin.Lhs, joinForFirstLeaf, output)) return false;
            var thisJoin = bin.Op == TokenKind.Or ? ConditionJoin.Or : ConditionJoin.And;
            if (!TryComparison(bin.Rhs, out var rhsRule)) return false;
            rhsRule.Join = thisJoin;
            output.Add(rhsRule);
            return true;
        }

        if (!TryComparison(node, out var leafRule)) return false;
        leafRule.Join = joinForFirstLeaf;
        output.Add(leafRule);
        return true;
    }

    private static BinaryNode MakeComparison(ConditionRule rule)
    {
        var opTok = rule.Operator switch
        {
            ConditionOperator.Eq    => TokenKind.EqEq,
            ConditionOperator.NotEq => TokenKind.NotEq,
            ConditionOperator.Lt    => TokenKind.Lt,
            ConditionOperator.Lte   => TokenKind.Lte,
            ConditionOperator.Gt    => TokenKind.Gt,
            ConditionOperator.Gte   => TokenKind.Gte,
            _ => TokenKind.EqEq,
        };
        return new BinaryNode(opTok, new VarNode(rule.Variable), new NumberNode(rule.Value));
    }

    private static bool TryComparison(Node node, out ConditionRule rule)
    {
        rule = new ConditionRule();
        if (node is not BinaryNode bin) return false;
        if (bin.Lhs is not VarNode v) return false;
        if (bin.Rhs is not NumberNode n) return false;

        ConditionOperator op;
        switch (bin.Op)
        {
            case TokenKind.EqEq:  op = ConditionOperator.Eq;    break;
            case TokenKind.NotEq: op = ConditionOperator.NotEq; break;
            case TokenKind.Lt:    op = ConditionOperator.Lt;    break;
            case TokenKind.Lte:   op = ConditionOperator.Lte;   break;
            case TokenKind.Gt:    op = ConditionOperator.Gt;    break;
            case TokenKind.Gte:   op = ConditionOperator.Gte;   break;
            default: return false;
        }

        rule.Variable = v.Name;
        rule.Operator = op;
        rule.Value = n.Value;
        return true;
    }
}
