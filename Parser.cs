using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniLISP
{
    public enum TokenType
    {
        EOF,
        // For reporting errors:
        Error,

        // The only punctuation:
        ParenOpen,
        ParenClose,
        BracketOpen,
        BracketClose,
        Quote,
        Dot,
        Slash,

        // Basic primitives:
        Identifier,

        Null,
        String,
        Boolean,

        Integer,
        Decimal,
        Double,
        Float,
    }

    public class Token
    {
        public readonly int Position;
        public readonly TokenType Type;
        public readonly string Text;

        public Token(int pos, TokenType type, string text)
        {
            Type = type;
            Position = pos;
            Text = text;
        }
    }

    /// <summary>
    /// MiniLISP lexer for the parser.
    /// </summary>
    public sealed class Lexer
    {
        readonly TextReader tr;
        int pos, lpos;
        int c;  // last character read

        public Lexer(TextReader tr, char readFirst = '\0')
        {
            if (tr == null) throw new ArgumentNullException("tr");
            this.tr = tr;

            // Assume initial position is 0 in the TextReader:
            this.pos = 0;
            this.lpos = 0;

            // Signal that we need to read the first char:
            if (readFirst == '\0')
                this.c = -2;
            else
                this.c = readFirst;
        }

        public int LastPosition { get { return pos; } }
        public int LastChar { get { return c; } }

        int Read()
        {
            // Don't attempt to read anything if we last read EOF:
            if (this.c == -1) return -1;

            lpos = pos;
            int c = tr.Read();
            if (c == -1) return -1;

            // Keep track of stream position:
            ++pos;
            return c;
        }

        public Token Next()
        {
            // Only read the first char on the first call to Next():
            if (c == -2) c = Read();

            do
            {
                // EOF:
                if (c == -1 || c == 0) return new Token(pos, TokenType.EOF, null);

                // Skip whitespace (commas are whitespace too):
                if (c == ' ' || c == '\t' || c == ',' || c == '\n' || c == '\r')
                {
                    c = Read();
                    continue;
                }

                // TODO(jsd): comments!

                // Curlies and parens are equivalent in this LISP:
                if (c == (int)'(' || c == (int)'{')
                {
                    var tok = new Token(lpos, TokenType.ParenOpen, ((char)c).ToString());
                    c = Read();
                    return tok;
                }
                else if (c == (int)')' || c == (int)'}')
                {
                    var tok = new Token(lpos, TokenType.ParenClose, ((char)c).ToString());
                    c = Read();
                    return tok;
                }
                // Square brackets denote plain lists of data, not to be eval'd:
                else if (c == (int)'[')
                {
                    var tok = new Token(lpos, TokenType.BracketOpen, ((char)c).ToString());
                    c = Read();
                    return tok;
                }
                else if (c == (int)']')
                {
                    var tok = new Token(lpos, TokenType.BracketClose, ((char)c).ToString());
                    c = Read();
                    return tok;
                }
                // Punctuation:
                else if (c == (int)'~')
                {
                    var tok = new Token(lpos, TokenType.Quote, ((char)c).ToString());
                    c = Read();
                    return tok;
                }
                else if (c == '.')
                {
                    var tok = new Token(lpos, TokenType.Dot, ((char)c).ToString());
                    c = Read();
                    return tok;
                }
                else if (c == '/')
                {
                    var tok = new Token(lpos, TokenType.Slash, ((char)c).ToString());
                    c = Read();
                    return tok;
                }
                // Identifiers:
                else if (Char.IsLetter((char)c) || c == '_')
                {
                    // Parse an identifier ([A-Za-z][A-Za-z0-9\-_]*):
                    int spos = lpos;

                    var sb = new StringBuilder(10);
                    do
                    {
                        sb.Append((char)c);
                        c = Read();
                        if (c == -1) break;
                    } while (Char.IsLetterOrDigit((char)c) || c == '-' || c == '_');

                    var ident = sb.ToString();
                    if (String.Equals(ident, "true"))
                        return new Token(spos, TokenType.Boolean, ident);
                    else if (String.Equals(ident, "false"))
                        return new Token(spos, TokenType.Boolean, ident);
                    else if (String.Equals(ident, "null"))
                        return new Token(spos, TokenType.Null, ident);
                    else
                        return new Token(spos, TokenType.Identifier, sb.ToString());
                }
                // Quoted string literals:
                else if (c == (int)'\'')
                {
                    int spos = lpos;
                    var sb = new StringBuilder(10);

                    do
                    {
                        c = Read();
                        if (c == -1) return new Token(pos, TokenType.Error, "Unexpected end");
                        else if (c == '\'') break;
                        else if (c == '\\')
                        {
                            c = Read();
                            if (c == -1) return new Token(pos, TokenType.Error, "Unexpected end");
                            else if (c == '\'') sb.Append('\'');
                            else if (c == '\\') sb.Append('\\');
                            else if (c == 'n') sb.Append('\n');
                            else if (c == 'r') sb.Append('\r');
                            else if (c == 't') sb.Append('\t');
                            else return new Token(pos, TokenType.Error, "Unknown backslash escape character '{0}'".F((char)c));
                        }
                        else sb.Append((char)c);
                    } while (true);

                    c = Read();
                    return new Token(spos, TokenType.String, sb.ToString());
                }
                // Raw string literals:
                else if (c == (int)'`')
                {
                    int spos = lpos;
                    var sb = new StringBuilder(10);

                    do
                    {
                        c = Read();
                        if (c == -1) return new Token(pos, TokenType.Error, "Unexpected end");
                        // TODO(jsd): Any escape sequences?
                        else if (c == '`') break;
                        else sb.Append((char)c);
                    } while (true);

                    c = Read();
                    return new Token(spos, TokenType.String, sb.ToString());
                }
                // Numerics:
                else if (Char.IsDigit((char)c) || c == '-')
                {
                    // Simple/stupid numeric parser [0-9]+(\.[0-9]+)?:
                    int spos = lpos;
                    bool hasDecimal = false;

                    var sb = new StringBuilder(10);
                    do
                    {
                        sb.Append((char)c);

                        c = Read();
                        if (c == -1) break;
                        if (c == '.')
                            hasDecimal = true;
                    } while (Char.IsDigit((char)c) || c == '.');

                    // Determine the type of number by suffix or presence of decimal point:
                    var type = TokenType.Integer;
                    if (c == 'd')
                    {
                        c = Read();
                        type = TokenType.Double;
                    }
                    else if (c == 'f')
                    {
                        c = Read();
                        type = TokenType.Float;
                    }
                    else if (hasDecimal)
                    {
                        // Default to decimal type if have a decimal point:
                        type = TokenType.Decimal;
                    }

                    return new Token(spos, type, sb.ToString());
                }
                else
                {
                    return new Token(lpos, TokenType.Error, "Unexpected character '{0}'".F((char)c));
                }
            } while (c != -1);

            return new Token(pos, TokenType.EOF, null);
        }
    }

    public sealed class ParserException : Exception
    {
        public readonly Token Token;

        public ParserException(Token tok, string message)
            : base("MiniLISP error(pos {0}): {1}".F(tok.Position, message))
        {
            Token = tok;
        }
    }

    public enum SExprKind
    {
        Error,

        Quote,
        Invocation,
        List,

        // global "identifier":
        ScopedIdentifier,
        // instance-specific ".memberName":
        InstanceMemberIdentifier,
        // static "namespace.class/member":
        StaticMemberIdentifier,

        String,
        Integer,
        Boolean,
        Null,
        Decimal,
        Double,
        Float,
    }

    public abstract class SExpr
    {
        public readonly SExprKind Kind;
        public readonly Token StartToken, EndToken;

        protected SExpr(SExprKind kind, Token start, Token end)
        {
            Kind = kind;
            StartToken = start;
            EndToken = end;
        }

        public void ThrowIfError()
        {
            if (Kind != SExprKind.Error) return;

            var err = (ParserError)this;
            throw new ParserException(StartToken, err.Message);
        }

        public virtual StringBuilder AppendTo(StringBuilder sb)
        {
            sb.Append("???");
            return sb;
        }
    }

    public sealed class ParserError : SExpr
    {
        public readonly string Message;

        public ParserError(Token where, string message)
            : base(SExprKind.Error, where, where)
        {
            Message = message;
        }

        public override StringBuilder AppendTo(StringBuilder sb)
        {
            sb.Append("(ERROR ");
            sb = StringExpr.Format(Message, sb);
            sb.Append(')');
            return sb;
        }
    }

    public abstract class IdentifierExpr : SExpr
    {
        protected IdentifierExpr(SExprKind kind, Token start, Token end)
            : base(kind, start, end)
        {
        }
    }

    public sealed class ScopedIdentifierExpr : IdentifierExpr
    {
        public readonly Token Name;

        public ScopedIdentifierExpr(Token ident)
            : base(SExprKind.ScopedIdentifier, ident, ident)
        {
            Name = ident;
        }

        public override StringBuilder AppendTo(StringBuilder sb)
        {
            sb.Append(Name.Text);
            return sb;
        }
    }

    public sealed class InstanceMemberIdentifierExpr : IdentifierExpr
    {
        public readonly Token Name;

        public InstanceMemberIdentifierExpr(Token dot, Token ident)
            : base(SExprKind.InstanceMemberIdentifier, dot, ident)
        {
            Name = ident;
        }

        public override StringBuilder AppendTo(StringBuilder sb)
        {
            sb.Append('.');
            sb.Append(Name.Text);
            return sb;
        }
    }

    public sealed class StaticMemberIdentifierExpr : IdentifierExpr
    {
        public readonly Token[] TypeName;
        public readonly Token Name;

        public StaticMemberIdentifierExpr(Token[] @typeName, Token ident)
            : base(SExprKind.StaticMemberIdentifier, @typeName[0], ident)
        {
            TypeName = @typeName;
            Name = ident;
        }

        public override StringBuilder AppendTo(StringBuilder sb)
        {
            sb.Append(String.Join(".", TypeName.Select(ns => ns.Text)));
            sb.Append('/');
            sb.Append(Name.Text);
            return sb;
        }
    }

    public sealed class InvocationExpr : SExpr
    {
        public readonly IdentifierExpr Identifier;
        public readonly SExpr[] Parameters;

        public InvocationExpr(Token start, Token end, IdentifierExpr identifier, params SExpr[] parameters)
            : base(SExprKind.Invocation, start, end)
        {
            Identifier = identifier;
            Parameters = parameters;
        }

        /// <summary>
        /// Gets the count of parameters.
        /// </summary>
        public int Count
        {
            get { return Parameters.Length; }
        }

        /// <summary>
        /// Gets the parameter s-expression at index <paramref name="index"/>.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public SExpr this[int index]
        {
            get { return Parameters[index]; }
        }

        public override StringBuilder AppendTo(StringBuilder sb)
        {
            sb.Append('(');
            sb = Identifier.AppendTo(sb);
            for (int i = 0; i < Count; ++i)
            {
                sb.Append(' ');
                sb = this[i].AppendTo(sb);
            }
            sb.Append(')');
            return sb;
        }
    }

    public sealed class ListExpr : SExpr
    {
        public readonly SExpr[] Items;

        public ListExpr(Token start, Token end, params SExpr[] items)
            : base(SExprKind.List, start, end)
        {
            Items = items;
        }

        /// <summary>
        /// Gets the count of list items.
        /// </summary>
        public int Count
        {
            get { return Items.Length; }
        }

        /// <summary>
        /// Gets the list item s-expression at index <paramref name="index"/>.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public SExpr this[int index]
        {
            get { return Items[index]; }
        }

        public override StringBuilder AppendTo(StringBuilder sb)
        {
            sb.Append('[');
            for (int i = 0; i < Count; ++i)
            {
                sb = this[i].AppendTo(sb);
                if (i < Count - 1) sb.Append(' ');
            }
            sb.Append(']');
            return sb;
        }
    }

    public sealed class QuoteExpr : SExpr
    {
        public readonly SExpr SExpr;

        public QuoteExpr(Token start, Token end, SExpr sexpr)
            : base(SExprKind.Quote, start, end)
        {
            SExpr = sexpr;
        }

        public override StringBuilder AppendTo(StringBuilder sb)
        {
            sb.Append('~');
            sb = SExpr.AppendTo(sb);
            return sb;
        }
    }

    public class IntegerExpr : SExpr
    {
        public readonly long Value;

        public IntegerExpr(Token token, long value)
            : base(SExprKind.Integer, token, token)
        {
            if (token.Type != TokenType.Integer) throw new ArgumentException("token must be of type Integer for an IntegerExpr");
            Value = value;
        }

        public override StringBuilder AppendTo(StringBuilder sb)
        {
            sb.Append(Value.ToString());
            return sb;
        }
    }

    public sealed class DecimalExpr : SExpr
    {
        public readonly decimal Value;

        public DecimalExpr(Token token, decimal value)
            : base(SExprKind.Decimal, token, token)
        {
            if (token.Type != TokenType.Decimal) throw new ArgumentException("token must be of type Decimal for a DecimalExpr");
            Value = value;
        }

        public override StringBuilder AppendTo(StringBuilder sb)
        {
            sb.Append(Value.ToString());
            return sb;
        }
    }

    public sealed class DoubleExpr : SExpr
    {
        public readonly double Value;

        public DoubleExpr(Token token, double value)
            : base(SExprKind.Double, token, token)
        {
            if (token.Type != TokenType.Double) throw new ArgumentException("token must be of type Double for a DoubleExpr");
            Value = value;
        }

        public override StringBuilder AppendTo(StringBuilder sb)
        {
            sb.Append(Value.ToString());
            sb.Append('d');
            return sb;
        }
    }

    public sealed class FloatExpr : SExpr
    {
        public readonly float Value;

        public FloatExpr(Token token, float value)
            : base(SExprKind.Float, token, token)
        {
            if (token.Type != TokenType.Float) throw new ArgumentException("token must be of type Float for a FloatExpr");
            Value = value;
        }

        public override StringBuilder AppendTo(StringBuilder sb)
        {
            sb.Append(Value.ToString());
            sb.Append('f');
            return sb;
        }
    }

    public sealed class StringExpr : SExpr
    {
        public readonly string Value;

        public StringExpr(Token token)
            : base(SExprKind.String, token, token)
        {
            if (token.Type != TokenType.String) throw new ArgumentException("token must be of type String for a StringExpr");
            Value = token.Text;
        }

        public override StringBuilder AppendTo(StringBuilder sb)
        {
            Format(Value, sb);
            return sb;
        }

        /// <summary>
        /// Formats the given string as a quoted string literal including backslash escape sequences.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="sb"></param>
        /// <returns></returns>
        public static StringBuilder Format(string value, StringBuilder sb = null)
        {
            if (sb == null) sb = new StringBuilder();
            sb.Append('\'');
            foreach (char c in value)
            {
                if (c == '\\')
                    sb.Append("\\\\");
                else if (c == '\'')
                    sb.Append("\\\'");
                else if (c == '\n')
                    sb.Append("\\n");
                else if (c == '\r')
                    sb.Append("\\r");
                else if (c == '\t')
                    sb.Append("\\t");
                else
                    sb.Append(c);
            }
            sb.Append('\'');
            return sb;
        }
    }

    public sealed class BooleanExpr : SExpr
    {
        public bool Value;

        public BooleanExpr(Token token, bool value)
            : base(SExprKind.Boolean, token, token)
        {
            if (token.Type != TokenType.Boolean) throw new ArgumentException("token must be of type Boolean for a BooleanExpr");
            Value = value;
        }

        public override StringBuilder AppendTo(StringBuilder sb)
        {
            if (Value)
                sb.Append("true");
            else
                sb.Append("false");
            return sb;
        }
    }

    public sealed class NullExpr : SExpr
    {
        public NullExpr(Token token)
            : base(SExprKind.Null, token, token)
        {
            if (token.Type != TokenType.Null) throw new ArgumentException("token must be of type Null for a NullExpr");
        }

        public override StringBuilder AppendTo(StringBuilder sb)
        {
            sb.Append("null");
            return sb;
        }
    }

    /// <summary>
    /// MiniLISP parser.
    /// </summary>
    public sealed class Parser
    {
        readonly Lexer lex;
        // Last read token:
        Token tok;
        // Last parser state:
        Either<Token, ParserError> next;
        bool hold = false;

        public Parser(Lexer lex)
        {
            if (lex == null) throw new ArgumentNullException("lex");
            this.lex = lex;
            this.tok = null;
        }

        void Hold()
        {
            hold = true;
        }

        void Next()
        {
            if (hold)
            {
                hold = false;
                return;
            }
            tok = lex.Next();

            if (tok.Type == TokenType.EOF) next = new ParserError(tok, "Unexpected end");
            else if (tok.Type == TokenType.Error) next = new ParserError(tok, tok.Text);
            else next = tok;
        }

        void Expect(TokenType type)
        {
            Next();
            if (next.IsRight) return;

            Debug.Assert(next.IsLeft);
            Debug.Assert(next.Left != null);

            // Check the token type is the expected type:
            if (next.Left.Type != type) next = new ParserError(tok, "Unexpected token '{0}', expecting '{1}'".F(next.Left.Type, type));
        }

        void ExpectOr(TokenType type1, TokenType type2)
        {
            Next();
            if (next.IsRight) return;

            Debug.Assert(next.IsLeft);
            Debug.Assert(next.Left != null);

            // Check the token type is the expected type:
            if (next.Left.Type == type1) return;
            else if (next.Left.Type == type2) return;
            else next = new ParserError(tok, "Unexpected token '{0}', expecting '{1}' or '{2}'".F(next.Left.Type, type1, type2));
        }

        public SExpr ParseExpr()
        {
            Next();
            if (next.IsRight) return next.Right;

            if (tok.Type == TokenType.ParenOpen)
            {
                var start = tok;

                // NOTE(jsd): Built-in identifier expression parser here. Probably should extract this for general purpose usage.

                // Expect function name:
                ExpectOr(TokenType.Identifier, TokenType.Dot);
                if (next.IsRight) return next.Right;

                var identStart = tok;
                IdentifierExpr ident;

                // "memberName"
                // ".memberName"
                // "namespace.Type/memberName"

                if (next.Left.Type == TokenType.Dot)
                {
                    // ".memberName":
                    Expect(TokenType.Identifier);
                    if (next.IsRight) return next.Right;

                    ident = new InstanceMemberIdentifierExpr(identStart, next.Left);

                    Next();
                    if (next.IsRight) return next.Right;
                }
                else
                {
                    // "memberName"

                    var typeNameParts = new List<Token>(5);
                    typeNameParts.Add(next.Left);

                    // "namespace.typeName" ?
                    Next();
                    while (!next.IsRight && next.Left.Type == TokenType.Dot)
                    {
                        Next();
                        if (next.Left.Type == TokenType.Slash)
                            break;
                        if (next.Left.Type != TokenType.Identifier)
                            break;

                        typeNameParts.Add(next.Left);

                        Next();
                    };
                    if (next.IsRight) return next.Right;

                    // Determine what type of identifier:
                    if (next.Left.Type == TokenType.Slash)
                    {
                        // "namespace.typeName/memberName"
                        Expect(TokenType.Identifier);
                        if (next.IsRight) return next.Right;

                        ident = new StaticMemberIdentifierExpr(typeNameParts.ToArray(), next.Left);

                        Next();
                        if (next.IsRight) return next.Right;
                    }
                    else
                    {
                        if (typeNameParts.Count != 1)
                            return new ParserError(tok, "Scoped identifier expression must have only one identifier part");

                        ident = new ScopedIdentifierExpr(typeNameParts[0]);
                    }
                }

                // Parse parameters:
                List<SExpr> parameters;
                if (tok.Type == TokenType.ParenClose)
                {
                    // No parameters:
                    parameters = new List<SExpr>(0);
                }
                else
                {
                    // At least one parameter:
                    parameters = new List<SExpr>();

                    do
                    {
                        Hold();
                        var expr = ParseExpr();
                        Debug.Assert(expr != null);
                        if (expr.Kind == SExprKind.Error) return expr;

                        parameters.Add(expr);
                        Next();
                    } while (tok.Type != TokenType.ParenClose);
                }

                var end = tok;

                return new InvocationExpr(start, end, ident, parameters.ToArray());
            }
            else if (tok.Type == TokenType.BracketOpen)
            {
                var start = tok;

                // Parse items:
                List<SExpr> items;

                Next();
                if (next.IsRight) return next.Right;
                if (tok.Type == TokenType.BracketClose)
                {
                    // No items:
                    items = new List<SExpr>(0);
                }
                else
                {
                    // At least one item:
                    items = new List<SExpr>();

                    do
                    {
                        Hold();
                        var expr = ParseExpr();
                        Debug.Assert(expr != null);
                        if (expr.Kind == SExprKind.Error) return expr;

                        items.Add(expr);
                        Next();
                    } while (tok.Type != TokenType.BracketClose);
                }

                var end = tok;

                return new ListExpr(start, end, items.ToArray());
            }
            else if (tok.Type == TokenType.Quote)
            {
                var start = tok;

                var expr = ParseExpr();
                if (expr.Kind == SExprKind.Error) return expr;

                var end = tok;

                return new QuoteExpr(start, end, expr);
            }
            else if (tok.Type == TokenType.Identifier)
            {
                var expr = new ScopedIdentifierExpr(tok);
                return expr;
            }
            else if (tok.Type == TokenType.Integer)
            {
                long val;

                if (!Int64.TryParse(tok.Text, out val))
                    return new ParserError(tok, "Could not parse '{0}' as an Int64".F(tok.Text));

                var expr = new IntegerExpr(tok, val);
                return expr;
            }
            else if (tok.Type == TokenType.String)
            {
                var expr = new StringExpr(tok);
                return expr;
            }
            else if (tok.Type == TokenType.Boolean)
            {
                bool val;
                if (!Boolean.TryParse(tok.Text, out val))
                    return new ParserError(tok, "Could not parse '{0}' as a boolean".F(tok.Text));

                var expr = new BooleanExpr(tok, val);
                return expr;
            }
            else if (tok.Type == TokenType.Null)
            {
                var expr = new NullExpr(tok);
                return expr;
            }
            else if (tok.Type == TokenType.Decimal)
            {
                decimal val;

                if (!Decimal.TryParse(tok.Text, out val))
                    return new ParserError(tok, "Could not parse '{0}' as a decimal".F(tok.Text));

                var expr = new DecimalExpr(tok, val);
                return expr;
            }
            else if (tok.Type == TokenType.Double)
            {
                double val;

                if (!Double.TryParse(tok.Text, out val))
                    return new ParserError(tok, "Could not parse '{0}' as a double".F(tok.Text));

                var expr = new DoubleExpr(tok, val);
                return expr;
            }
            else if (tok.Type == TokenType.Float)
            {
                float val;

                if (!Single.TryParse(tok.Text, out val))
                    return new ParserError(tok, "Could not parse '{0}' as a float".F(tok.Text));

                var expr = new FloatExpr(tok, val);
                return expr;
            }

            return new ParserError(tok, "Unexpected token '{0}'".F(tok.Type));
        }
    }
}
