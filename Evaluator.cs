using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniLISP
{
    public delegate object ExternFunction(Evaluator v, InvocationExpr e);

    public sealed class Evaluator : IEnumerable<KeyValuePair<string, ExternFunction>>
    {
        Dictionary<string, ExternFunction> externs;

        public Evaluator()
        {
            // Define standard external functions:
            externs = new Dictionary<string, ExternFunction>()
            {
                { "eval", StandardExternFunctions.Eval },
                { "if", StandardExternFunctions.If },
                { "eq", StandardExternFunctions.Eq },
                { "ne", StandardExternFunctions.Ne },
            };
        }

        public void Add(string name, ExternFunction func)
        {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException("name", "name cannot be empty or null");
            if (func == null)
                throw new ArgumentNullException("func", "func cannot be null");

            // Check for existence:
            if (externs.ContainsKey(name))
                throw new InvalidOperationException("A function with the name '{0}' is already defined".F(name));

            // Define the custom function:
            externs.Add(name, func);
        }

        public IEnumerator<KeyValuePair<string, ExternFunction>> GetEnumerator()
        {
            return externs.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return externs.GetEnumerator();
        }

        public T EvalExpecting<T>(SExpr sexpr)
        {
            object val = Eval(sexpr);

            if (val.GetType() != typeof(T))
                throw new Exception("Excepting a value of type {0} but got type {1}".F(typeof(T), val.GetType()));

            return (T)val;
        }

        public object Eval(SExpr sexpr)
        {
            sexpr.ThrowIfError();

            if (sexpr.Kind == SExprKind.Identifier)
            {
                return sexpr.StartToken.Text;
            }
            else if (sexpr.Kind == SExprKind.Invocation)
            {
                return Invoke((InvocationExpr)sexpr);
            }
            else if (sexpr.Kind == SExprKind.List)
            {
                var le = (ListExpr)sexpr;
                var items = new object[le.Count];
                for (int i = 0; i < items.Length; ++i)
                {
                    items[i] = Eval(le[i]);
                }
                return items;
            }
            else if (sexpr.Kind == SExprKind.Quote)
            {
                return sexpr;
            }
            else if (sexpr.Kind == SExprKind.Integer)
            {
                return ((IntegerExpr)sexpr).Value;
            }
            else if (sexpr.Kind == SExprKind.Boolean)
            {
                return ((BooleanExpr)sexpr).Value;
            }
            else if (sexpr.Kind == SExprKind.Null)
            {
                return null;
            }
            else if (sexpr.Kind == SExprKind.String)
            {
                return sexpr.StartToken.Text;
            }
            else if (sexpr.Kind == SExprKind.Decimal)
            {
                return ((DecimalExpr)sexpr).Value;
            }
            else if (sexpr.Kind == SExprKind.Double)
            {
                return ((DoubleExpr)sexpr).Value;
            }
            else if (sexpr.Kind == SExprKind.Float)
            {
                return ((FloatExpr)sexpr).Value;
            }

            throw new Exception("Unknown expression kind: '{0}'".F(sexpr.Kind));
        }

        public object[] Eval(SExpr[] sexprs)
        {
            if (sexprs == null) return null;
            if (sexprs.Length == 0) return new object[0];

            var results = new object[sexprs.Length];
            for (int i = 0; i < sexprs.Length; ++i)
            {
                results[i] = Eval(sexprs[i]);
            }
            return results;
        }

        public object Invoke(InvocationExpr e)
        {
            // Check the function name:
            var name = e.FuncName.Text;
            // TODO(jsd): Support CLR property/method invocation for objects!

            // Find the function in the `externs` dictionary:
            ExternFunction func;
            if (!externs.TryGetValue(name, out func))
                throw new Exception("Undefined function '{0}'".F(name));

            // Execute it:
            return func(this, e);
        }
    }

    public static class StandardExternFunctions
    {
        public static object Eval(Evaluator v, InvocationExpr e)
        {
            if (e.Count != 1) throw new Exception("`eval` requires 1 parameter");
            
            var quoteExpr = e[0] as QuoteExpr;
            if (quoteExpr == null) throw new Exception("`eval` parameter must be a quoted s-expression");

            return v.Eval(((QuoteExpr)quoteExpr).SExpr);
        }

        public static object If(Evaluator v, InvocationExpr e)
        {
            if (e.Count != 3) throw new Exception("`if` requires 3 parameters: condition, then, else");

            var testExpr = e[0];
            var trueExpr = e[1];
            var falseExpr = e[2];

            // Eval the test s-expression:
            var test = v.Eval(testExpr);
            if (test == null)
            {
                // Null is logically false:
                return v.Eval(falseExpr);
            }
            else
            {
                // Check if the test value is a boolean or not:
                if (test is bool)
                {
                    // Test the boolean value:
                    if ((bool)test)
                        return v.Eval(trueExpr);
                    else
                        return v.Eval(falseExpr);
                }
                else if (!test.GetType().IsValueType)
                {
                    // Non-null reference is logically true:
                    return v.Eval(trueExpr);
                }
                else
                    throw new Exception("`if` condition parameter must be a boolean or reference type");
            }
        }

        public static object Eq(Evaluator v, InvocationExpr e)
        {
            if (e.Count != 2) throw new Exception("`eq` requires 2 parameters");

            var a = v.Eval(e[0]);
            var b = v.Eval(e[1]);

            if (a == null && b == null)
                return true;
            else if (a == null || b == null)
                return false;
            else
                return a.Equals(b);
        }

        public static object Ne(Evaluator v, InvocationExpr e)
        {
            if (e.Count != 2) throw new Exception("`ne` requires 2 parameters");

            var a = v.Eval(e[0]);
            var b = v.Eval(e[1]);

            if (a == null && b == null)
                return false;
            else if (a == null || b == null)
                return true;
            else
                return !a.Equals(b);
        }
    }
}
