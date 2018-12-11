using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;
using System;
using System.Collections.Generic;

namespace Frisia.Solver
{
    public class Z3Solver : ISolver
    {
        public string[] TryGetModel(SeparatedSyntaxList<ParameterSyntax> parameters, IList<ExpressionSyntax> conditions)
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

        public string[] GetModel(SeparatedSyntaxList<ParameterSyntax> parameters, IList<ExpressionSyntax> conditions)
        {
            if (parameters.Count == 0)
            {
                return new string[parameters.Count];
            }

            using (var ctx = new Context(new Dictionary<string, string> { { "model", "true" } }))
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

        private BoolExpr[] GetBranch(Context ctx, SeparatedSyntaxList<ParameterSyntax> parameters, IList<ExpressionSyntax> conditions)
        {
            var branch = new BoolExpr[conditions.Count];

            using (var convert = new Z3Converter(ctx, parameters))
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    try
                    {
                        branch[i] = (BoolExpr)convert.ToExpr(conditions[i]);
                    }
                    catch (NotSupportedException)
                    {
                        // If not supported, set as true
                        branch[i] = ctx.MkTrue();
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
            using (var solver = ctx.MkSolver())
            {
                solver.Assert(constraints);
                switch (solver.Check())
                {
                    case Status.UNSATISFIABLE:
                        return null;
                    case Status.SATISFIABLE:
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
