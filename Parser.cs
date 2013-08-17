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

        // Basic primitives:
        Identifier,
        String,
        Integer
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

    public sealed class Lexer
    {
        readonly TextReader tr;
        int pos, lpos, wpos;
        int c;  // last character read

        public Lexer(TextReader tr)
        {
            if (tr == null) throw new ArgumentNullException("tr");
            this.tr = tr;

            // Assume initial position is 0 in the TextReader:
            this.pos = 0;
            this.lpos = 0;
            this.wpos = 0;

            // Signal that we need to read the first char:
            this.c = -2;
        }

        /// <summary>
        /// Last position that was parsed which contained a non-whitespace char.
        /// </summary>
        public int LastPosition { get { return wpos; } }

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

            while (true)
            {
                // EOF:
                if (c == -1 || c == 0) return new Token(pos, TokenType.EOF, null);

                // Skip whitespace:
                wpos = lpos;
                while (c == ' ' || c == '\n' || c == '\r')
                    c = Read();

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
                    return new Token(spos, TokenType.Integer, sb.ToString());
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

                    return new Token(spos, TokenType.Identifier, sb.ToString());
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
                else
                {
                    return new Token(lpos, TokenType.Error, "Unexpected character '{0}'".F((char)c));
                }
            }
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

        Invocation,
        List,

        Identifier,
        String,
        Integer
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
    }

    public sealed class ParserError : SExpr
    {
        public readonly string Message;

        public ParserError(Token where, string message)
            : base(SExprKind.Error, where, where)
        {
            Message = message;
        }
    }

    public sealed class InvocationExpr : SExpr
    {
        public readonly Token FuncName;
        public readonly SExpr[] Parameters;

        public InvocationExpr(Token start, Token end, Token funcName, params SExpr[] parameters)
            : base(SExprKind.Invocation, start, end)
        {
            FuncName = funcName;
            Parameters = parameters;
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
    }

    public sealed class IdentifierExpr : SExpr
    {
        public IdentifierExpr(Token token)
            : base(SExprKind.Identifier, token, token)
        {
            if (token.Type != TokenType.Identifier) throw new ArgumentException("token must be of type Identifier for an IdentifierExpr");
        }
    }

    public sealed class IntegerExpr : SExpr
    {
        public IntegerExpr(Token token)
            : base(SExprKind.Integer, token, token)
        {
            if (token.Type != TokenType.Integer) throw new ArgumentException("token must be of type Integer for an IntegerExpr");
        }
    }

    public sealed class StringExpr : SExpr
    {
        public StringExpr(Token token)
            : base(SExprKind.String, token, token)
        {
            if (token.Type != TokenType.String) throw new ArgumentException("token must be of type String for a StringExpr");
        }
    }

    public sealed class Parser
    {
        readonly Lexer lex;
        // Last read token:
        Token tok;
        // Last parser state:
        Either<Token, ParserError> next;

        public Parser(Lexer lex)
        {
            if (lex == null) throw new ArgumentNullException("lex");
            this.lex = lex;
            this.tok = null;
        }

        void Next()
        {
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

        public SExpr ParseExpr()
        {
            if (tok == null)
            {
                Next();
            }
            if (next.IsRight) return next.Right;

            if (tok.Type == TokenType.ParenOpen)
            {
                var start = tok;

                // Expect function name:
                Expect(TokenType.Identifier);
                if (next.IsRight) return next.Right;
                var funcName = next.Left;

                // Parse parameters:
                List<SExpr> parameters;

                Next();
                if (next.IsRight) return next.Right;
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
                        var expr = ParseExpr();
                        Debug.Assert(expr != null);
                        if (expr.Kind == SExprKind.Error) return expr;

                        parameters.Add(expr);
                    } while (tok.Type != TokenType.ParenClose);
                }

                var end = tok;

                Next();
                return new InvocationExpr(start, end, funcName, parameters.ToArray());
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
                        var expr = ParseExpr();
                        Debug.Assert(expr != null);
                        if (expr.Kind == SExprKind.Error) return expr;

                        items.Add(expr);
                    } while (tok.Type != TokenType.BracketClose);
                }

                var end = tok;

                Next();
                return new ListExpr(start, end, items.ToArray());
            }
            else if (tok.Type == TokenType.Identifier)
            {
                var expr = new IdentifierExpr(tok);
                Next();
                return expr;
            }
            else if (tok.Type == TokenType.Integer)
            {
                var expr = new IntegerExpr(tok);
                Next();
                return expr;
            }
            else if (tok.Type == TokenType.String)
            {
                var expr = new StringExpr(tok);
                Next();
                return expr;
            }

            return new ParserError(tok, "Unexpected token '{0}'".F(tok.Type));
        }
    }
}
