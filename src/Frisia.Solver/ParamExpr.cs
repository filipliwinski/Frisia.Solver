using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;

namespace Frisia.Solver
{
    internal class ParamExpr
    {
        public Expr Expr { get; private set; }
        public PredefinedTypeSyntax Type { get; private set; }

        public ParamExpr(Expr expr, PredefinedTypeSyntax type)
        {
            Expr = expr;
            Type = type;
        }
    }
}
