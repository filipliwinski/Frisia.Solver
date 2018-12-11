﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;
using System;
//using System.Collections.Generic;

namespace Frisia.Solver
{
    public class Z3Solver : ISolver
    {
        public string[] TryGetModel(SeparatedSyntaxList<ParameterSyntax> parameters, System.Collections.Generic.IList<ExpressionSyntax> conditions)
        {
            try
            {
                return GetModel(parameters, conditions);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public string[] GetModel(SeparatedSyntaxList<ParameterSyntax> parameters, System.Collections.Generic.IList<ExpressionSyntax> conditions)
        {
            if (parameters.Count == 0)
            {
                return new string[parameters.Count];
            }

            using (var ctx = new Context(new System.Collections.Generic.Dictionary<string, string> { { "model", "true" } }))
            {
                var paramsSet = GetParamsSet(ctx, parameters);

                var branch = GetBranch(ctx, parameters, conditions);
                return ResolveBranch(ctx, branch, paramsSet);
            }
        }

        private Expr[] GetParamsSet(Context ctx, SeparatedSyntaxList<ParameterSyntax> parameters)
        {
            var paramsSet = new Expr[parameters.Count];

            using (var convert = new Z3Converter(ctx, parameters))
            {
                for (int i = 0; i < paramsSet.Length; i++)
                {
                    paramsSet[i] = convert.ToExpr(parameters[i]);
                }
            }

            return paramsSet;
        }

        private BoolExpr[] GetBranch(Context ctx, SeparatedSyntaxList<ParameterSyntax> parameters, System.Collections.Generic.IList<ExpressionSyntax> conditions)
        {
            var branch = new BoolExpr[conditions.Count];

            using (var convert = new Z3Converter(ctx, parameters))
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    try
                    {
                        branch[i] = (BoolExpr)convert.ToExpr(conditions[i]);
                        //using (var constraint = (BoolExpr)convert.ToExpr(conditions[i]))
                        //{
                        //    branch = ctx.MkAnd(constraint, branch);
                        //}                        
                    }
                    catch (NotSupportedException)
                    {
                        // If not supported, set as true
                        branch[i] = ctx.MkTrue();
                        //branch = ctx.MkAnd(, branch);
                    }
                }
            }

            return branch;
        }
        
        private string[] ResolveBranch(Context ctx, BoolExpr[] branch, Expr[] paramsSet)
        {
            var result = new string[paramsSet.Length];

            using (var model = Solve(ctx, branch))
            {
                if (model != null)
                {
                    for (int i = 0; i < paramsSet.Length; i++)
                    {
                        using (var expr = model.Evaluate(paramsSet[i]))
                        {
                            // If parameter is not evaluated in the model, set default value
                            if (paramsSet[i] == expr)
                            {
                                result[i] = GetDefaultValue(expr).ToString();
                            }
                            else
                            {
                                result[i] = expr.ToString();
                            }
                        }                        
                    }

                    return result;
                }
            }

            return null;
        }

        private Model Solve(Context ctx, BoolExpr[] constraints)
        {
            //Console.WriteLine(typeof(Context).Assembly.FullName + " " + typeof(Context).Assembly.GetName().Version);

            //var p1 = ctx.MkIntConst("a1");
            //var p2 = ctx.MkIntConst("n1");
            //var a = ctx.MkNot(ctx.MkLe(p2, ctx.MkInt(0)));
            //var b = ctx.MkLt(ctx.MkAdd(p1, ctx.MkInt(1)), p2);
            //var c = ctx.MkEq(ctx.MkMod((IntExpr)ctx.MkAdd(p1, ctx.MkInt(1)), ctx.MkInt(5)), ctx.MkInt(0));
            //var d = ctx.MkNot(ctx.MkEq(ctx.MkMod((IntExpr)ctx.MkAdd(p1, ctx.MkInt(1)), ctx.MkInt(7)), ctx.MkInt(0)));
            using (var solver = ctx.MkSolver())
            {
                //solver.Assert(a, b, c, d);
                solver.Assert(constraints);
                Console.WriteLine("SAT check:\n" + solver.ToString());
                switch (solver.Check(/*constraints*/))
                {
                    case Status.UNSATISFIABLE:
                        return null;
                    case Status.SATISFIABLE:
                        Console.WriteLine("Model:\n" + solver.Model);
                        return solver.Model;
                    default:
                        throw new Z3Exception("Unknown satisfiability.");
                }
            }
        }

        private object GetDefaultValue(Expr expr)
        {
            if (expr.IsInt) return default(int);
            if (expr.IsBool) return default(bool);
            throw new Z3Exception("Unsupported type.");
        }
    }
}
