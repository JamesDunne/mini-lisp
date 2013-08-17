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

            string[] codes = new string[]
            {
                // All should error:
                @"",
                @"(",
                @")",
                @"[",
                @"]",
                @"'",
                @"'\'",
                @"'\b'",
                @"(true)",
                @"(null)",
                @"(())",
                @"([] ())",
                @"`",
                @"-",

                // All should succeed:
                @"true",
                @"false",
                @"null",
                @"'test'",
                @"`(if true true false)",
                @"(eval `(if true true false))",
                @"(if true 'hello' 'world')",
                @"(if false 'hello' 'world')",
                @"(if (eq true false) yes no)",
                @"(if (eq false false) yes no)",
                @"(if (eq false false) true null)",
                @"(if (eq null null) true null)",
                @"(if (ne true false) yes no)",
                @"(if (ne false false) yes no)",
                @"(if (ne false false) true null)",
                @"(if (ne null null) true null)",
                @"'hello
world'",
                @"'hello\nworld'",
                @"'hello\t\rworld'",
                @"'hello \'world\''",
                @"'hello ""world""'",
                @"`'test'",
                @"`1.34",
                @"`1.34d",
                @"`1.34f",
                @"1.333333333333333333333333333333333333",
                @"1.333333333333333333333333333333333333d",
                @"1.333333333333333333333333333333333333f",
                @"1",
                @"008",
                @"-1",
                @"[10 -3]",
            };

            var ev = new Evaluator();
            for (int i = 0; i < codes.Length; ++i)
            {
                string code = codes[i];
                try
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("[{0,3}]: ", i);
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    var prs = new Parser(new Lexer(new StringReader(code)));
                    var expr = prs.ParseExpr();
                    // Output the s-expression:
                    var sb = new StringBuilder();
                    Console.WriteLine(expr.AppendTo(sb).ToString());
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("=> ");
                    Console.ForegroundColor = ConsoleColor.White;
                    // Evaluate and output:
                    var result = ev.Eval(expr);
                    Output(result);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write(ex);
                    Console.ForegroundColor = ConsoleColor.White;
                }
                Console.WriteLine();
            }
        }

        static void Output(object result)
        {
            object[] results;
            if (result == null)
            {
                Console.Write("null");
            }
            else if (result is string)
            {
                var quotedStr = StringExpr.Format((string)result).ToString();
                Console.Write(quotedStr);
            }
            else if ((results = result as object[]) != null)
            {
                Console.Write('[');
                for (int i = 0; i < results.Length; ++i)
                {
                    Output(results[i]);
                    if (i < results.Length - 1) Console.Write(' ');
                }
                Console.Write(']');
            }
            else if (result is bool)
            {
                if ((bool)result)
                    Console.Write("true");
                else
                    Console.Write("false");
            }
            else if (result is SExpr)
            {
                var sexpr = (SExpr)result;
                var sb = new StringBuilder();
                sb = sexpr.AppendTo(sb);
                Console.Write(sb.ToString());
            }
            else
            {
                Console.Write(result.ToString());
            }
        }

        static void TestCustomExterns()
        {
            // Create an evaluator with custom defined functions:
            var ev = new Evaluator()
            {
                { "str", (v, e) => v.Eval(e[0]).ToString() },
                { "qualify", (v, e) =>
                {
                    if (e.Count != 2) throw new ArgumentException("qualify requires 2 parameters");

                    // Evaluate parameters:
                    var prefix = v.EvalExpecting<string>(e[0]);
                    var list = v.EvalExpecting<object[]>(e[1]);

                    var sb = new StringBuilder();
                    for (int i = 0; i < list.Length; ++i)
                    {
                        if (list[i].GetType() != typeof(string)) throw new ArgumentException("list item {0} must evaluate to a string".F(i + 1));
                        sb.AppendFormat("[{0}].[{1}]", prefix, (string)list[i]);
                        if (i < list.Length - 1) sb.Append(", ");
                    }
                    return sb.ToString();
                } },
                { "prefix", (v, e) =>
                {
                    if (e.Count != 2) throw new ArgumentException("prefix requires 2 parameters");

                    // Evaluate parameters:
                    var prefix = v.EvalExpecting<string>(e[0]);
                    var list = v.EvalExpecting<object[]>(e[1]);

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

            // Run through some test cases:
            {
                const string code = @"{prefix st [StudentID FirstName LastName]}";
                var prs = new Parser(new Lexer(new StringReader(code)));
                var expr = prs.ParseExpr();
                // Evaluate and output:
                var result = ev.Eval(expr);
                Output(result);
                Console.WriteLine();
            }
        }
    }
}
