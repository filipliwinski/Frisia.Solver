using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace Frisia.Solver
{
    public interface ISolver
    {
        string[] GetModel(SeparatedSyntaxList<ParameterSyntax> parameters, IList<ExpressionSyntax> conditions);
        string[] TryGetModel(SeparatedSyntaxList<ParameterSyntax> parameters, IList<ExpressionSyntax> conditions);
    }
}