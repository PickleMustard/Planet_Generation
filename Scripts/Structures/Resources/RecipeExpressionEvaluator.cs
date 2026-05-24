using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Structures.Resources;

/// <summary>
/// Evaluates math + boolean expressions over a fixed five-variable context for
/// recipe conditional outputs. Variables: temperature, moisture, elevation,
/// atmosphere, specifier. Parsed ASTs are cached per source string.
/// </summary>
public static class RecipeExpressionEvaluator
{
    public static readonly IReadOnlyList<string> AllowedVariables = new[]
    {
        "temperature",
        "moisture",
        "elevation",
        "atmosphere",
        "specifier",
    };

    private static readonly ConcurrentDictionary<string, Node> _cache = new();

    public static Node Compile(string expression)
    {
        if (expression is null)
            throw new RecipeExpressionException("Recipe condition expression is null.");

        return _cache.GetOrAdd(expression, src =>
        {
            var tokens = Tokenize(src);
            var parser = new Parser(tokens, src);
            var root = parser.ParseExpression();
            parser.ExpectEnd();
            return root;
        });
    }

    public static bool EvaluateBool(Node node, IReadOnlyDictionary<string, float> ctx)
        => node.Evaluate(ctx) > 0.5f;

    // ---- Token model ----------------------------------------------------

    internal enum TokenKind
    {
        Number, Ident, LParen, RParen,
        Plus, Minus, Star, Slash, Percent,
        Lt, Lte, Gt, Gte, EqEq, NotEq,
        And, Or, Not,
        End,
    }

    private readonly struct Token
    {
        public readonly TokenKind Kind;
        public readonly string Text;
        public readonly float Number;
        public readonly int Position;
        public Token(TokenKind kind, string text, int pos, float number = 0f)
        {
            Kind = kind; Text = text; Position = pos; Number = number;
        }
    }

    private static List<Token> Tokenize(string src)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < src.Length)
        {
            char c = src[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (char.IsDigit(c) || (c == '.' && i + 1 < src.Length && char.IsDigit(src[i + 1])))
            {
                int start = i;
                while (i < src.Length && (char.IsDigit(src[i]) || src[i] == '.')) i++;
                string lit = src.Substring(start, i - start);
                if (!float.TryParse(lit, NumberStyles.Float, CultureInfo.InvariantCulture, out float num))
                    throw new RecipeExpressionException($"Invalid numeric literal '{lit}' at position {start} in '{src}'.");
                tokens.Add(new Token(TokenKind.Number, lit, start, num));
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                int start = i;
                while (i < src.Length && (char.IsLetterOrDigit(src[i]) || src[i] == '_')) i++;
                string ident = src.Substring(start, i - start);
                tokens.Add(new Token(TokenKind.Ident, ident, start));
                continue;
            }

            int pos = i;
            switch (c)
            {
                case '(': tokens.Add(new Token(TokenKind.LParen, "(", pos)); i++; break;
                case ')': tokens.Add(new Token(TokenKind.RParen, ")", pos)); i++; break;
                case '+': tokens.Add(new Token(TokenKind.Plus, "+", pos)); i++; break;
                case '-': tokens.Add(new Token(TokenKind.Minus, "-", pos)); i++; break;
                case '*': tokens.Add(new Token(TokenKind.Star, "*", pos)); i++; break;
                case '/': tokens.Add(new Token(TokenKind.Slash, "/", pos)); i++; break;
                case '%': tokens.Add(new Token(TokenKind.Percent, "%", pos)); i++; break;
                case '<':
                    if (Peek(src, i + 1) == '=') { tokens.Add(new Token(TokenKind.Lte, "<=", pos)); i += 2; }
                    else { tokens.Add(new Token(TokenKind.Lt, "<", pos)); i++; }
                    break;
                case '>':
                    if (Peek(src, i + 1) == '=') { tokens.Add(new Token(TokenKind.Gte, ">=", pos)); i += 2; }
                    else { tokens.Add(new Token(TokenKind.Gt, ">", pos)); i++; }
                    break;
                case '=':
                    if (Peek(src, i + 1) != '=')
                        throw new RecipeExpressionException($"Unexpected '=' at position {pos} in '{src}' (did you mean '=='?).");
                    tokens.Add(new Token(TokenKind.EqEq, "==", pos)); i += 2; break;
                case '!':
                    if (Peek(src, i + 1) == '=') { tokens.Add(new Token(TokenKind.NotEq, "!=", pos)); i += 2; }
                    else { tokens.Add(new Token(TokenKind.Not, "!", pos)); i++; }
                    break;
                case '&':
                    if (Peek(src, i + 1) != '&')
                        throw new RecipeExpressionException($"Unexpected '&' at position {pos} in '{src}' (did you mean '&&'?).");
                    tokens.Add(new Token(TokenKind.And, "&&", pos)); i += 2; break;
                case '|':
                    if (Peek(src, i + 1) != '|')
                        throw new RecipeExpressionException($"Unexpected '|' at position {pos} in '{src}' (did you mean '||'?).");
                    tokens.Add(new Token(TokenKind.Or, "||", pos)); i += 2; break;
                default:
                    throw new RecipeExpressionException($"Unexpected character '{c}' at position {pos} in '{src}'.");
            }
        }
        tokens.Add(new Token(TokenKind.End, string.Empty, src.Length));
        return tokens;
    }

    private static char Peek(string s, int i) => i < s.Length ? s[i] : '\0';

    // ---- AST -----------------------------------------------------------

    public abstract class Node
    {
        public abstract float Evaluate(IReadOnlyDictionary<string, float> ctx);
    }

    internal sealed class NumberNode : Node
    {
        internal readonly float Value;
        public NumberNode(float v) { Value = v; }
        public override float Evaluate(IReadOnlyDictionary<string, float> ctx) => Value;
    }

    internal sealed class VarNode : Node
    {
        internal readonly string Name;
        public VarNode(string name) { Name = name; }
        public override float Evaluate(IReadOnlyDictionary<string, float> ctx)
            => ctx.TryGetValue(Name, out float v) ? v : 0f;
    }

    internal sealed class UnaryNode : Node
    {
        internal readonly TokenKind Op;
        internal readonly Node Rhs;
        public UnaryNode(TokenKind op, Node rhs) { Op = op; Rhs = rhs; }
        public override float Evaluate(IReadOnlyDictionary<string, float> ctx)
        {
            float v = Rhs.Evaluate(ctx);
            return Op switch
            {
                TokenKind.Minus => -v,
                TokenKind.Not => v > 0.5f ? 0f : 1f,
                _ => throw new RecipeExpressionException($"Unknown unary operator {Op}.")
            };
        }
    }

    internal sealed class BinaryNode : Node
    {
        internal readonly TokenKind Op;
        internal readonly Node Lhs, Rhs;
        public BinaryNode(TokenKind op, Node lhs, Node rhs) { Op = op; Lhs = lhs; Rhs = rhs; }
        public override float Evaluate(IReadOnlyDictionary<string, float> ctx)
        {
            // Short-circuit boolean ops avoid evaluating the rhs unnecessarily.
            if (Op == TokenKind.And) return (Lhs.Evaluate(ctx) > 0.5f && Rhs.Evaluate(ctx) > 0.5f) ? 1f : 0f;
            if (Op == TokenKind.Or)  return (Lhs.Evaluate(ctx) > 0.5f || Rhs.Evaluate(ctx) > 0.5f) ? 1f : 0f;
            float a = Lhs.Evaluate(ctx);
            float b = Rhs.Evaluate(ctx);
            return Op switch
            {
                TokenKind.Plus    => a + b,
                TokenKind.Minus   => a - b,
                TokenKind.Star    => a * b,
                TokenKind.Slash   => b == 0f ? 0f : a / b,
                TokenKind.Percent => b == 0f ? 0f : a % b,
                TokenKind.Lt      => a <  b ? 1f : 0f,
                TokenKind.Lte     => a <= b ? 1f : 0f,
                TokenKind.Gt      => a >  b ? 1f : 0f,
                TokenKind.Gte     => a >= b ? 1f : 0f,
                TokenKind.EqEq    => Math.Abs(a - b) < 1e-6f ? 1f : 0f,
                TokenKind.NotEq   => Math.Abs(a - b) < 1e-6f ? 0f : 1f,
                _ => throw new RecipeExpressionException($"Unknown binary operator {Op}.")
            };
        }
    }

    // ---- Parser --------------------------------------------------------

    private sealed class Parser
    {
        private readonly List<Token> _tokens;
        private readonly string _src;
        private int _pos;

        public Parser(List<Token> tokens, string src) { _tokens = tokens; _src = src; _pos = 0; }

        private Token Peek() => _tokens[_pos];
        private Token Consume() => _tokens[_pos++];
        private bool Match(TokenKind k) { if (Peek().Kind == k) { _pos++; return true; } return false; }

        public void ExpectEnd()
        {
            if (Peek().Kind != TokenKind.End)
                throw new RecipeExpressionException($"Unexpected trailing token '{Peek().Text}' at position {Peek().Position} in '{_src}'.");
        }

        public Node ParseExpression() => ParseOr();

        private Node ParseOr()
        {
            var node = ParseAnd();
            while (Peek().Kind == TokenKind.Or)
            {
                var op = Consume().Kind;
                node = new BinaryNode(op, node, ParseAnd());
            }
            return node;
        }

        private Node ParseAnd()
        {
            var node = ParseNot();
            while (Peek().Kind == TokenKind.And)
            {
                var op = Consume().Kind;
                node = new BinaryNode(op, node, ParseNot());
            }
            return node;
        }

        private Node ParseNot()
        {
            if (Peek().Kind == TokenKind.Not)
            {
                Consume();
                return new UnaryNode(TokenKind.Not, ParseNot());
            }
            return ParseComparison();
        }

        private Node ParseComparison()
        {
            var node = ParseAdditive();
            var k = Peek().Kind;
            if (k == TokenKind.Lt || k == TokenKind.Lte || k == TokenKind.Gt
                || k == TokenKind.Gte || k == TokenKind.EqEq || k == TokenKind.NotEq)
            {
                Consume();
                node = new BinaryNode(k, node, ParseAdditive());
            }
            return node;
        }

        private Node ParseAdditive()
        {
            var node = ParseMultiplicative();
            while (Peek().Kind == TokenKind.Plus || Peek().Kind == TokenKind.Minus)
            {
                var op = Consume().Kind;
                node = new BinaryNode(op, node, ParseMultiplicative());
            }
            return node;
        }

        private Node ParseMultiplicative()
        {
            var node = ParseUnary();
            while (Peek().Kind == TokenKind.Star || Peek().Kind == TokenKind.Slash || Peek().Kind == TokenKind.Percent)
            {
                var op = Consume().Kind;
                node = new BinaryNode(op, node, ParseUnary());
            }
            return node;
        }

        private Node ParseUnary()
        {
            if (Peek().Kind == TokenKind.Minus)
            {
                Consume();
                return new UnaryNode(TokenKind.Minus, ParseUnary());
            }
            return ParsePrimary();
        }

        private Node ParsePrimary()
        {
            var tok = Peek();
            switch (tok.Kind)
            {
                case TokenKind.Number:
                    Consume();
                    return new NumberNode(tok.Number);
                case TokenKind.Ident:
                    Consume();
                    if (!IsAllowedVariable(tok.Text))
                        throw new RecipeExpressionException(
                            $"Unknown identifier '{tok.Text}' at position {tok.Position} in '{_src}'. " +
                            $"Allowed: {string.Join(", ", AllowedVariables)}.");
                    return new VarNode(tok.Text);
                case TokenKind.LParen:
                    Consume();
                    var inner = ParseExpression();
                    if (!Match(TokenKind.RParen))
                        throw new RecipeExpressionException($"Expected ')' at position {Peek().Position} in '{_src}'.");
                    return inner;
                default:
                    throw new RecipeExpressionException(
                        $"Unexpected token '{tok.Text}' at position {tok.Position} in '{_src}'.");
            }
        }

        private static bool IsAllowedVariable(string name)
        {
            foreach (var v in AllowedVariables)
                if (string.Equals(v, name, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}

public sealed class RecipeExpressionException : Exception
{
    public RecipeExpressionException(string message) : base(message) { }
    public RecipeExpressionException(string message, Exception inner) : base(message, inner) { }
}
