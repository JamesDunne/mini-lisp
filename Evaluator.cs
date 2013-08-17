using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniLISP
{
    public delegate object ExternFunction(Evaluator eval, InvocationExpr invexp);

    public sealed class Evaluator : IEnumerable<KeyValuePair<string, ExternFunction>>
    {
        Dictionary<string, ExternFunction> externs;

        public Evaluator()
        {
            externs = new Dictionary<string, ExternFunction>();
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

        public object Invoke(InvocationExpr invexp)
        {
            // Find the function in the `externs` dictionary:
            ExternFunction func;
            var name = invexp.FuncName.Text;
            if (!externs.TryGetValue(name, out func))
                throw new Exception("Undefined function '{0}'".F(name));

            // Execute it:
            return func(this, invexp);
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

            if (sexpr.Kind == SExprKind.Integer)
            {
                return Int32.Parse(sexpr.StartToken.Text);
            }
            else if (sexpr.Kind == SExprKind.String)
            {
                return sexpr.StartToken.Text;
            }
            else if (sexpr.Kind == SExprKind.Identifier)
            {
                return sexpr.StartToken.Text;
            }
            else if (sexpr.Kind == SExprKind.List)
            {
                var le = (ListExpr)sexpr;
                var items = new object[le.Items.Length];
                for (int i = 0; i < items.Length; ++i)
                {
                    items[i] = Eval(le.Items[i]);
                }
                return items;
            }
            else if (sexpr.Kind == SExprKind.Invocation)
            {
                return Invoke((InvocationExpr)sexpr);
            }

            throw new InvalidOperationException();
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
    }
}
