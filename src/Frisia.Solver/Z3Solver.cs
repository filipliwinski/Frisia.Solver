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

        private Expr[] GetBranch(Context ctx, SeparatedSyntaxList<ParameterSyntax> parameters, IList<ExpressionSyntax> conditions)
        {
            var branch = new Expr[conditions.Count];

            using (var convert = new Z3Converter(ctx, parameters))
            {
                for (int i = 0; i < branch.Length; i++)
                {
                    try
                    {
                        branch[i] = convert.ToExpr(conditions[i]);
                    }
                    catch (NotSupportedException)
                    {
                        branch[i] = ctx.MkTrue();
                    }
                }
            }

            return branch;
        }
        
        private string[] ResolveBranch(Context ctx, Expr[] branch, Expr[] paramsSet)
        {
            var result = new string[paramsSet.Length];
            var constraints = ctx.MkTrue();

            for (int i = 0; i < branch.Length; i++)
            {
                constraints = ctx.MkAnd((BoolExpr)branch[i], constraints);
            }

            var model = Solve(ctx, constraints);

            if (model != null)
            {
                for (int i = 0; i < paramsSet.Length; i++)
                {
                    result[i] = model.Evaluate(paramsSet[i]).ToString();
                }

                return result;
            }

            return null;
        }

        private Model Solve(Context ctx, BoolExpr constraints)
        {
            var solver = ctx.MkSolver();
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
}
