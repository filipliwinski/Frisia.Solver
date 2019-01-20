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

        private ParamExpr[] GetParamsSet(Context ctx, SeparatedSyntaxList<ParameterSyntax> parameters)
        {
            var paramsSet = new ParamExpr[parameters.Count];

            using (var convert = new Z3Converter(ctx, parameters))
            {
                for (int i = 0; i < paramsSet.Length; i++)
                {
                    paramsSet[i] = new ParamExpr(convert.ToExpr(parameters[i]), parameters[i].Type as PredefinedTypeSyntax);
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

        private string[] ResolveBranch(Context ctx, BoolExpr[] branch, ParamExpr[] paramsSet)
        {
            var result = new string[paramsSet.Length];

            using (var model = Solve(ctx, branch))
            {
                if (model != null)
                {
                    for (int i = 0; i < paramsSet.Length; i++)
                    {
                        using (var expr = model.Evaluate(paramsSet[i].Expr))
                        {
                            // If parameter is not evaluated in the model, set default value
                            if (paramsSet[i].Expr == expr)
                            {
                                result[i] = GetDefaultValue(expr).ToString();
                            }
                            else
                            {
                                result[i] = CheckBoundaries(expr, paramsSet[i].Type);
                            }
                        }
                    }

                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves branch with boundaries constraints. Not used due to StackOverflow for samples.
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="branch"></param>
        /// <param name="paramsSet"></param>
        /// <returns></returns>
        private string[] ResolveBranchWithBoundaries(Context ctx, BoolExpr[] branch, ParamExpr[] paramsSet)
        {
            var result = new string[paramsSet.Length];

            var count = 0;
            var numParamsSet = new List<ParamExpr>();

            foreach (var p in paramsSet)
            {
                if (p.Expr.IsInt)
                {
                    count++;
                    numParamsSet.Add(p);
                }
            }

            var extBranch = new BoolExpr[branch.Length + count * 2];

            for (int i = 0; i < branch.Length; i++)
            {
                extBranch[i] = branch[i];
            }

            // Add min and max boundaries
            for (int i = 0; i < count; i++)
            {
                extBranch[branch.Length + i * 2] = GetMinExpr(ctx, paramsSet[i]);
                extBranch[branch.Length + i * 2 + 1] = GetMaxExpr(ctx, paramsSet[i]);
            }

            using (var model = Solve(ctx, extBranch))
            {
                if (model != null)
                {
                    for (int i = 0; i < paramsSet.Length; i++)
                    {
                        using (var expr = model.Evaluate(paramsSet[i].Expr))
                        {
                            // If parameter was not evaluated in the model, set default value
                            if (paramsSet[i].Expr == expr)
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
            throw new NotSupportedException("Unsupported type.");
        }
        
        private BoolExpr GetMinExpr(Context ctx, ParamExpr expr)
        {
            if (expr.Expr.IsInt)
            {
                long value = 0;
                switch (expr.Type.Keyword.Text)
                {
                    case "byte":
                        value = byte.MinValue;
                        break;
                    case "short":
                        value = short.MinValue;
                        break;
                    case "int":
                        value = int.MinValue;
                        break;
                    case "long":
                        value = long.MinValue;
                        break;
                    default:
                        break;
                }
                return ctx.MkGe((ArithExpr)expr.Expr, ctx.MkInt(value));
            }
            throw new NotSupportedException("Unsupported type.");
        }

        private BoolExpr GetMaxExpr(Context ctx, ParamExpr expr)
        {
            long value = 0;
            switch (expr.Type.Keyword.Text)
            {
                case "byte":
                    value = byte.MaxValue;
                    break;
                case "short":
                    value = short.MaxValue;
                    break;
                case "int":
                    value = int.MaxValue;
                    break;
                case "long":
                    value = long.MaxValue;
                    break;
                default:
                    break;
            }
            return ctx.MkLe((ArithExpr)expr.Expr, ctx.MkInt(value));
        }

        private string CheckBoundaries(Expr expr, PredefinedTypeSyntax type)
        {
            if (expr.IsInt)
            {
                var value = Convert.ToInt64(expr.ToString());
                switch (type.Keyword.Text)
                {
                    case "byte":
                        if (value < byte.MinValue)
                            return byte.MinValue.ToString();
                        if (value > byte.MaxValue)
                            return byte.MaxValue.ToString();
                        break;
                    case "short":
                        if (value < short.MinValue)
                            return short.MinValue.ToString();
                        if (value > short.MaxValue)
                            return short.MaxValue.ToString();
                        break;
                    case "int":
                        if (value < int.MinValue)
                            return int.MinValue.ToString();
                        if (value > int.MaxValue)
                            return int.MaxValue.ToString();
                        break;
                    case "long":
                        if (value < long.MinValue)
                            return long.MinValue.ToString();
                        if (value > long.MaxValue)
                            return long.MaxValue.ToString();
                        break;
                    default:
                        break;
                }
            }
            return expr.ToString();
        }
    }
}