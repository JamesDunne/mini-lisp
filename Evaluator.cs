using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniLISP
{
    public delegate object ExternFunction(Evaluator v, InvocationExpr e);
    public delegate object ExternEvaluate(Evaluator v, SExpr e, ExternEvaluate eval);

    public sealed class Evaluator : IEnumerable<KeyValuePair<string, ExternFunction>>
    {
        public class Storage
        {
            public Type Type;
            public object Value;

            public Storage(Type type, object value = null)
            {
                if (type == null) throw new ArgumentNullException("type");

                Type = type;
                Value = value;
            }
        }

        public sealed class NamedStorage : Storage
        {
            public readonly string Name;

            public NamedStorage(string name, Type type, object value = null)
                : base(type, value)
            {
                if (String.IsNullOrEmpty(name)) throw new ArgumentNullException("name");

                Name = name;
            }
        }

        sealed class VariableScope
        {
            public readonly VariableScope Parent;
            public readonly Dictionary<string, NamedStorage> Variables;

            public VariableScope(VariableScope parent)
            {
                Parent = parent;
                Variables = new Dictionary<string, NamedStorage>();
            }

            public bool TryGetVariable(string name, out NamedStorage variable)
            {
                // Do we have it?
                if (Variables.TryGetValue(name, out variable))
                    return true;

                // Search the parent scope:
                if (Parent != null)
                    return Parent.TryGetVariable(name, out variable);

                // Not found:
                return false;
            }

            internal void Add(NamedStorage variable)
            {
                Variables.Add(variable.Name, variable);
            }
        }

        Dictionary<string, ExternFunction> externs;
        VariableScope globalScope;
        VariableScope scope;

        public Evaluator()
        {
            scope = globalScope = new VariableScope(null);

            // Define standard external functions:
            externs = new Dictionary<string, ExternFunction>()
            {
                { "eval", StandardExternFunctions.Eval },
                { "if", StandardExternFunctions.If },
                { "eq", StandardExternFunctions.Eq },
                { "ne", StandardExternFunctions.Ne },
            };
        }

        public void AddGlobal(NamedStorage variable)
        {
            globalScope.Add(variable);
        }

        /// <summary>
        /// Defines an external function.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="func"></param>
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

        public T EvalExpecting<T>(SExpr sexpr, ExternEvaluate customEval)
        {
            object val = customEval(this, sexpr, customEval);

            if (val.GetType() != typeof(T))
                throw new Exception("Excepting a value of type {0} but got type {1}".F(typeof(T), val.GetType()));

            return (T)val;
        }

        public T EvalExpecting<T>(SExpr sexpr)
        {
            object val = Eval(sexpr, null);

            if (val.GetType() != typeof(T))
                throw new Exception("Excepting a value of type {0} but got type {1}".F(typeof(T), val.GetType()));

            return (T)val;
        }

        public NamedStorage TryResolve(ScopedIdentifierExpr sexpr)
        {
            NamedStorage variable;
            var name = sexpr.StartToken.Text;
            if (!scope.TryGetVariable(name, out variable))
                return null;
            return variable;
        }

        object defaultEval(Evaluator a, SExpr sexpr, ExternEvaluate customEval)
        {
            return Eval(sexpr, customEval);
        }

        public object Eval(SExpr sexpr, ExternEvaluate customEval)
        {
            if (customEval == null)
                customEval = defaultEval;

            sexpr.ThrowIfError();

            if (sexpr.Kind == SExprKind.ScopedIdentifier)
            {
                // Try to resolve the identifier:
                var identExpr = (ScopedIdentifierExpr)sexpr;
                NamedStorage variable = TryResolve(identExpr);
                if (variable == null)
                    throw new Exception("Cannot find variable named '{0}' in scope".F(identExpr.Name));

                return variable.Value;
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
                    items[i] = customEval(this, le[i], customEval);
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

        public object Eval(SExpr sexpr)
        {
            return Eval(sexpr, defaultEval);
        }

        public object[] Eval(SExpr[] sexprs, ExternEvaluate customEval)
        {
            if (sexprs == null) return null;
            if (sexprs.Length == 0) return new object[0];
            if (customEval == null) customEval = defaultEval;

            var results = new object[sexprs.Length];
            for (int i = 0; i < sexprs.Length; ++i)
            {
                results[i] = customEval(this, sexprs[i], customEval);
            }
            return results;
        }

        public object[] Eval(SExpr[] sexprs)
        {
            return Eval(sexprs, defaultEval);
        }

        public object Invoke(InvocationExpr e)
        {
            if (e.Identifier.Kind == SExprKind.ScopedIdentifier)
            {
                // Check the function name:
                var name = ((ScopedIdentifierExpr)e.Identifier).Name.Text;
                // TODO(jsd): Support CLR property/method invocation for objects!

                // Find the function in the `externs` dictionary:
                ExternFunction func;
                if (!externs.TryGetValue(name, out func))
                    throw new Exception("Undefined function '{0}'".F(name));

                // Execute it:
                return func(this, e);
            }
            else if (e.Identifier.Kind == SExprKind.StaticMemberIdentifier)
            {
                // Find the type by namespace/class:
                var ident = (StaticMemberIdentifierExpr)e.Identifier;

                var typeName = String.Join(".", ident.TypeName.Select(ns => ns.Text));
                var memberName = ident.Name.Text;

                var type = Type.GetType(typeName, false);
                if (type == null)
                    throw new Exception("Could not find type by name '{0}'".F(typeName));

                // Member can be either a method or property:

                // Find a method:
                var mt = type.GetMethod(memberName);
                if (mt != null)
                {
                    object[] parms;

                    if (e.Count > 0)
                        parms = Eval(e.Parameters);
                    else
                        parms = null;

                    // TODO(jsd): handle invocation exception:
                    return mt.Invoke(null, parms);
                }

                // Find a property:
                var pr = type.GetProperty(memberName);
                if (pr != null)
                {
                    // TODO(jsd): Turn this into a property reference expression so we can (set x) it.
                    if (e.Count > 0)
                    {
                        var parms = Eval(e.Parameters);

                        return pr.GetValue(null, parms);
                    }
                    else
                    {
                        return pr.GetValue(null);
                    }
                }

                throw new Exception("Could not find method or property with name '{0}' on instance of type '{1}'".F(memberName, type.FullName));
            }
            else if (e.Identifier.Kind == SExprKind.InstanceMemberIdentifier)
            {
                // Find the type by namespace/class:
                var ident = (InstanceMemberIdentifierExpr)e.Identifier;

                var memberName = ident.Name.Text;

                var instance = Eval(e[0]);
                if (instance == null)
                    throw new Exception("Cannot call method or property '{0}' on a null object instance".F(memberName));

                var type = instance.GetType();

                // Member can be either a method or property:

                // Evaluate parameter expressions:
                var parmExprs = e.Parameters.Skip(1).ToArray();
                object[] parms;
                Type[] parmTypes;
                if (parmExprs.Length > 0)
                {
                    parms = Eval(parmExprs);
                    parmTypes = parms.Select(p => p == null ? (Type)null : p.GetType()).ToArray();
                }
                else
                {
                    parms = null;
                    parmTypes = Type.EmptyTypes;
                }

                // Find a method:
                var mt = type.GetMethod(memberName, parmTypes);
                if (mt != null)
                {
                    // TODO(jsd): handle invocation exception:
                    return mt.Invoke(instance, parms);
                }

                // Find a property:
                var pr = type.GetProperty(memberName);
                if (pr != null)
                {
                    // TODO(jsd): Turn this into a property reference expression so we can (set x) it.
                    return pr.GetValue(instance, parms);
                }

                throw new Exception("Could not find method or property with name '{0}' on instance of type '{1}'".F(memberName, type.FullName));
            }

            throw new Exception("Unknown or unsupported member identifier kind '{0}'".F(e.Identifier.Kind));
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
