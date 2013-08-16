using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniLISP
{
    public enum LISPTokenType
    {
        EOF,
        // For reporting errors:
        Error,

        // The only punctuation:
        ParenOpen,
        ParenClose,
        BracketOpen,
        BracketClose,

        // Basic primitives:
        Identifier,
        String,
        Integer
    }

    public class LISPToken
    {
        public readonly int Position;
        public readonly LISPTokenType Type;
        public readonly string Text;

        public LISPToken(int pos, LISPTokenType type, string text)
        {
            Type = type;
            Position = pos;
            Text = text;
        }
    }

    public sealed class LISPLexer
    {
        readonly TextReader tr;
        int pos, lpos;
        int c;  // last character read

        public LISPLexer(TextReader tr)
        {
            if (tr == null) throw new ArgumentNullException("tr");
            this.tr = tr;

            // Assume initial position is 0 in the TextReader:
            this.pos = 0;
            this.lpos = 0;

            // Signal that we need to read the first char:
            this.c = -2;
        }

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

        public LISPToken Next()
        {
            // Only read the first char on the first call to Next():
            if (c == -2) c = Read();

            do
            {
                // EOF:
                if (c == -1 || c == 0) return new LISPToken(pos, LISPTokenType.EOF, null);

                // Skip whitespace:
                if (c == ' ' || c == '\n' || c == '\r')
                {
                    c = Read();
                    continue;
                }

                // TODO(jsd): comments!

                // Curlies and parens are equivalent in this LISP:
                if (c == (int)'(' || c == (int)'{')
                {
                    var tok = new LISPToken(lpos, LISPTokenType.ParenOpen, ((char)c).ToString());
                    c = Read();
                    return tok;
                }
                else if (c == (int)')' || c == (int)'}')
                {
                    var tok = new LISPToken(lpos, LISPTokenType.ParenClose, ((char)c).ToString());
                    c = Read();
                    return tok;
                }
                // Square brackets denote plain lists of data, not to be eval'd:
                else if (c == (int)'[')
                {
                    var tok = new LISPToken(lpos, LISPTokenType.BracketOpen, ((char)c).ToString());
                    c = Read();
                    return tok;
                }
                else if (c == (int)']')
                {
                    var tok = new LISPToken(lpos, LISPTokenType.BracketClose, ((char)c).ToString());
                    c = Read();
                    return tok;
                }
                // Just integers for now, no decimals or floats:
                else if (Char.IsDigit((char)c))
                {
                    // Simple/stupid integer parser [0-9]+:
                    int spos = lpos;
                    var sb = new StringBuilder(10);
                    do
                    {
                        sb.Append((char)c);
                        c = Read();
                        if (c == -1) break;
                    } while (Char.IsDigit((char)c));
                    return new LISPToken(spos, LISPTokenType.Integer, sb.ToString());
                }
                // Identifiers:
                else if (Char.IsLetter((char)c) || c == (int)'-')
                {
                    // Parse an identifier:
                    // ([A-Za-z][A-Za-z0-9\-]*)
                    int spos = lpos;
                    var sb = new StringBuilder(10);
                    do
                    {
                        sb.Append((char)c);
                        c = Read();
                        if (c == -1) break;
                    } while (Char.IsLetterOrDigit((char)c) || c == (int)'-');

                    return new LISPToken(spos, LISPTokenType.Identifier, sb.ToString());
                }
                // Quoted strings:
                // NOTE(jsd): We avoid double quotes since we will be writing this language in a C# const string literal.
                else if (c == (int)'\'')
                {
                    int spos = lpos;
                    var sb = new StringBuilder(10);

                    do
                    {
                        c = Read();
                        if (c == -1) return new LISPToken(pos, LISPTokenType.Error, "Unexpected end");
                        else if (c == '\'') break;
                        else if (c == '\\')
                        {
                            c = Read();
                            if (c == -1) return new LISPToken(pos, LISPTokenType.Error, "Unexpected end");
                            else if (c == '\'') sb.Append('\'');
                            else if (c == '\\') sb.Append('\\');
                            else if (c == 'n') sb.Append('\n');
                            else if (c == 'r') sb.Append('\r');
                            else if (c == 't') sb.Append('\t');
                            else return new LISPToken(pos, LISPTokenType.Error, "Unknown backslash escape character '{0}'".F((char)c));
                        }
                        else sb.Append((char)c);
                    } while (true);

                    c = Read();
                    return new LISPToken(spos, LISPTokenType.String, sb.ToString());
                }
                else
                {
                    return new LISPToken(lpos, LISPTokenType.Error, "Unexpected character '{0}'".F((char)c));
                }
            } while (c != -1);

            return new LISPToken(pos, LISPTokenType.EOF, null);
        }
    }

    public enum SExprKind
    {
        Error,

        Invocation,
        List,

        Identifier,
        String,
        Integer
    }

    public abstract class SExpr
    {
        public readonly SExprKind Kind;
        public readonly LISPToken StartToken, EndToken;

        protected SExpr(SExprKind kind, LISPToken start, LISPToken end)
        {
            Kind = kind;
            StartToken = start;
            EndToken = end;
        }
    }

    public sealed class ParserError : SExpr
    {
        public readonly string Message;

        public ParserError(LISPToken where, string message)
            : base(SExprKind.Error, where, where)
        {
            Message = message;
        }
    }

    public sealed class InvocationExpr : SExpr
    {
        public readonly LISPToken FuncName;
        public readonly SExpr[] Parameters;

        public InvocationExpr(LISPToken start, LISPToken end, LISPToken funcName, params SExpr[] parameters)
            : base(SExprKind.Invocation, start, end)
        {
            FuncName = funcName;
            Parameters = parameters;
        }
    }

    public sealed class ListExpr : SExpr
    {
        public readonly SExpr[] Items;

        public ListExpr(LISPToken start, LISPToken end, params SExpr[] items)
            : base(SExprKind.List, start, end)
        {
            Items = items;
        }
    }

    public sealed class IdentifierExpr : SExpr
    {
        public readonly SExpr[] Items;

        public IdentifierExpr(LISPToken token)
            : base(SExprKind.Identifier, token, token)
        {
            if (token.Type != LISPTokenType.Identifier) throw new ArgumentException("token must be of type Identifier for an IdentifierExpr");
        }
    }

    public sealed class IntegerExpr : SExpr
    {
        public readonly SExpr[] Items;

        public IntegerExpr(LISPToken token)
            : base(SExprKind.Integer, token, token)
        {
            if (token.Type != LISPTokenType.Integer) throw new ArgumentException("token must be of type Integer for an IntegerExpr");
        }
    }

    public sealed class StringExpr : SExpr
    {
        public readonly SExpr[] Items;

        public StringExpr(LISPToken token)
            : base(SExprKind.String, token, token)
        {
            if (token.Type != LISPTokenType.String) throw new ArgumentException("token must be of type String for a StringExpr");
        }
    }

    public sealed class LISPParser
    {
        readonly LISPLexer lex;
        // Last read token:
        LISPToken tok;
        Either<LISPToken, ParserError> next;

        public LISPParser(LISPLexer lex)
        {
            if (lex == null) throw new ArgumentNullException("lex");
            this.lex = lex;
            this.tok = null;
        }

        void Next()
        {
            tok = lex.Next();

            if (tok.Type == LISPTokenType.EOF) next = new ParserError(tok, "Unexpected end");
            else if (tok.Type == LISPTokenType.Error) next = new ParserError(tok, tok.Text);
            else next = tok;
        }

        void Expect(LISPTokenType type)
        {
            Next();
            if (next.IsRight) return;

            Debug.Assert(next.IsLeft);
            Debug.Assert(next.Left != null);

            // Check the token type is the expected type:
            if (next.Left.Type != type) next = new ParserError(tok, "Unexpected token '{0}', expecting '{1}'".F(next.Left.Type, type));
        }

        public SExpr ParseExpr()
        {
            if (tok == null)
            {
                Next();
            }
            if (next.IsRight) return next.Right;

            if (tok.Type == LISPTokenType.ParenOpen)
            {
                var start = tok;

                // Expect function name:
                Expect(LISPTokenType.Identifier);
                if (next.IsRight) return next.Right;
                var funcName = next.Left;

                // Parse parameters:
                List<SExpr> parameters;

                Next();
                if (next.IsRight) return next.Right;
                if (tok.Type == LISPTokenType.ParenClose)
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
                        var expr = ParseExpr();
                        Debug.Assert(expr != null);
                        if (expr.Kind == SExprKind.Error) return expr;

                        parameters.Add(expr);
                    } while (tok.Type != LISPTokenType.ParenClose);
                }

                var end = tok;

                Next();
                return new InvocationExpr(start, end, funcName, parameters.ToArray());
            }
            else if (tok.Type == LISPTokenType.BracketOpen)
            {
                var start = tok;

                // Parse items:
                List<SExpr> items;

                Next();
                if (next.IsRight) return next.Right;
                if (tok.Type == LISPTokenType.BracketClose)
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
                        var expr = ParseExpr();
                        Debug.Assert(expr != null);
                        if (expr.Kind == SExprKind.Error) return expr;

                        items.Add(expr);
                    } while (tok.Type != LISPTokenType.BracketClose);
                }

                var end = tok;

                Next();
                return new ListExpr(start, end, items.ToArray());
            }
            else if (tok.Type == LISPTokenType.Identifier)
            {
                var expr = new IdentifierExpr(tok);
                Next();
                return expr;
            }
            else if (tok.Type == LISPTokenType.Integer)
            {
                var expr = new IntegerExpr(tok);
                Next();
                return expr;
            }
            else if (tok.Type == LISPTokenType.String)
            {
                var expr = new StringExpr(tok);
                Next();
                return expr;
            }

            return new ParserError(tok, "Unexpected token '{0}'".F(tok.Type));
        }
    }
}
