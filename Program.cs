using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MiniLISP
{
    class Program
    {
        static void Main(string[] args)
        {
            // With apologies to Philip Greenspun.

            // Create an evaluator with defined functions:
            var ev = new Evaluator()
            {
                { "str", (v, e) => v.Eval(e.Parameters[0]).ToString() },
                { "prefix", (v, e) =>
                {
                    if (e.Parameters.Length != 2) throw new ArgumentException("prefix requires 2 parameters");

                    // Evaluate parameters:
                    var prefix = v.EvalExpecting<string>(e.Parameters[0]);
                    var list = v.EvalExpecting<object[]>(e.Parameters[1]);

                    var sb = new StringBuilder();
                    for (int i = 0; i < list.Length; ++i)
                    {
                        if (list[i].GetType() != typeof(string)) throw new ArgumentException("list item {0} must evaluate to a string".F(i + 1));
                        sb.AppendFormat("[{0}].[{1}]", prefix, (string)list[i]);
                        if (i < list.Length - 1) sb.Append(", ");
                    }
                    return sb.ToString();
                } },
                { "rename", (v, e) =>
                {
                    if (e.Parameters.Length != 2) throw new ArgumentException("expand requires 2 parameters");

                    // Evaluate parameters:
                    var prefix = v.EvalExpecting<string>(e.Parameters[0]);
                    var list = v.EvalExpecting<object[]>(e.Parameters[1]);

                    var sb = new StringBuilder();
                    for (int i = 0; i < list.Length; ++i)
                    {
                        if (list[i].GetType() != typeof(string)) throw new ArgumentException("list item {0} must evaluate to a string".F(i + 1));
                        sb.AppendFormat("[{0}].[{1}] AS [{0}_{1}]", prefix, (string)list[i]);
                        if (i < list.Length - 1) sb.Append(", ");
                    }
                    return sb.ToString();
                } }
            };

            // Define our test code:
            string f = @"[{rename ce [a b c '1' '2' '3' '14']} {prefix ce [ID a B C]}] testing";

            {
                var lex = new Lexer(new StringReader(f));
                var prs = new Parser(lex);
                var expr = prs.ParseExpr();
                Console.WriteLine(Format(expr));
                // Evaluate:
                var result = ev.EvalExpecting<object[]>(expr);
                // Output the result:
                Console.WriteLine(result[0]);
                Console.WriteLine(result[1]);
            }

            // LastPosition must be the last char position which parsed a non-whitespace char.
            //Console.WriteLine(lex.LastPosition);

            // Time it all:
            var sw = Stopwatch.StartNew();
            for (int j = 0; j < 500000; ++j)
            {
                var lex = new Lexer(new StringReader(f));
                var prs = new Parser(lex);
                var expr = prs.ParseExpr();
                var result = ev.EvalExpecting<object[]>(expr);
            }
            sw.Stop();
            Console.WriteLine("total {0,6}, per {1,8}", sw.ElapsedMilliseconds, sw.ElapsedMilliseconds / 500000.0);

        }

        static string Format(Token tok)
        {
            return "{0,5} {1,-13} {2}".F(tok.Position, tok.Type, tok.Text ?? "");
        }

        static string Format(SExpr expr)
        {
            if (expr.Kind == SExprKind.Error)
            {
                var err = (ParserError)expr;
                return "ERROR({0}): {1}".F(err.StartToken.Position, err.Message);
            }
            else if (expr.Kind == SExprKind.Invocation)
            {
                var iexpr = (InvocationExpr)expr;
                if (iexpr.Parameters.Length == 0)
                    return "({0})".F(iexpr.FuncName.Text);
                else
                    return "({0} {1})".F(iexpr.FuncName.Text, String.Join(" ", iexpr.Parameters.Select(e => Format(e))));
            }
            else if (expr.Kind == SExprKind.List)
            {
                var lexpr = (ListExpr)expr;
                return "[{0}]".F(String.Join(" ", lexpr.Items.Select(e => Format(e))));
            }
            else if (expr.Kind == SExprKind.Identifier)
            {
                var iexpr = (IdentifierExpr)expr;
                return iexpr.StartToken.Text;
            }
            else if (expr.Kind == SExprKind.Integer)
            {
                var iexpr = (IntegerExpr)expr;
                return iexpr.StartToken.Text;
            }
            else if (expr.Kind == SExprKind.String)
            {
                var iexpr = (StringExpr)expr;
                // TODO(jsd): Proper escape sequence rendering:
                return "'{0}'".F(iexpr.StartToken.Text);
            }
            return "???";
        }
    }
}
