using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
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

            string f = @"{expand ce [a b c 1 2 3 14 [] [(a 'hello' 'world')] []] a-b A -adf}";

            var lex = new LISPLexer(new StringReader(f));
#if false
            LISPToken tok;
            while ((tok = lex.Next()).Type != LISPTokenType.EOF)
            {
                Console.WriteLine("{0,5} {1,-13} {2}", tok.Position, tok.Type, tok.Text ?? "");
            }
#endif

            var prs = new LISPParser(lex);
            var expr = prs.ParseExpr();
            Console.WriteLine(Format(expr));
        }

        static string Format(LISPToken tok)
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
