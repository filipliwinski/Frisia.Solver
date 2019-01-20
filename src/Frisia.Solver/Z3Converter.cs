using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;
using System;
using System.Linq;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace Frisia.Solver
{
    internal sealed class Z3Converter : IDisposable
    {
        private readonly Context ctx;
        private readonly SeparatedSyntaxList<ParameterSyntax> parameters;

        public Z3Converter(Context ctx, 
            SeparatedSyntaxList<ParameterSyntax> parameters)
        {
            this.ctx = ctx;
            this.parameters = parameters;
        }

        internal Expr ToExpr(ParameterSyntax parameter)
        {
            return ToExpr(parameter.Identifier, parameter.Type);
        }

        internal Expr ToExpr(SyntaxNode node)
        {
            switch (node)
            {
                case ArrayTypeSyntax arrayType:
                    return ToExpr(arrayType);
                case BinaryExpressionSyntax binaryExpression:
                    return ToExpr(binaryExpression);
                case CastExpressionSyntax castExpression:
                    return ToExpr(castExpression);
                case IdentifierNameSyntax identifierName:
                    return ToExpr(identifierName);
                case InvocationExpressionSyntax invocationExpression:
                    return ToExpr(invocationExpression);
                case LiteralExpressionSyntax literalExpression:
                    return ToExpr(literalExpression);
                case MemberAccessExpressionSyntax memberAccessExpression:
                    return ToExpr(memberAccessExpression);
                case ParenthesizedExpressionSyntax parenthesizedExpression:
                    return ToExpr(parenthesizedExpression.Expression);
                case PrefixUnaryExpressionSyntax prefixUnaryExpression:
                    return ToExpr(prefixUnaryExpression);
                default:
                    throw new NotImplementedException(node.GetType().Name);
            }
        }

        internal Expr ToExpr(InvocationExpressionSyntax node)
        {
            throw new NotSupportedException(node.GetType().Name + " is not supported.");
        }

        internal Expr ToExpr(PrefixUnaryExpressionSyntax node)
        {
            var expression = ToExpr(node.Operand);

            switch (node.Kind())
            {
                case LogicalNotExpression:
                    return ctx.MkNot((BoolExpr)expression);
                case UnaryMinusExpression:
                    return ctx.MkUnaryMinus((ArithExpr)expression);
                default:
                    throw new NotImplementedException(node.GetType().Name);
            }
        }

        internal Expr ToExpr(CastExpressionSyntax node)
        {
            return ToExpr(node.Expression);
        }

        internal Expr ToExpr(ArrayTypeSyntax node)
        {
            switch (node.ElementType.ToString())
            {
                case "string":
                    return ctx.MkConstArray(ctx.StringSort, ctx.MkString(""));
                default:
                    throw new NotImplementedException(node.ElementType + "[]");
            }
        }

        internal Expr ToExpr(IdentifierNameSyntax node)
        {
            try
            {
                var parameter = parameters.Single(x => x.Identifier.ValueText == node.Identifier.ValueText);

                return ToExpr(node.Identifier, parameter.Type);
            }
            catch (InvalidOperationException)
            {
                throw new NotSupportedException(node.Identifier.Text + " is not supported.");
            }
        }

        internal Expr ToExpr(LiteralExpressionSyntax node)
        {
            switch (node.Kind())
            {
                case NumericLiteralExpression:
                    if (node.Token.Value is byte)
                        return ctx.MkInt((byte)node.Token.Value);
                    if (node.Token.Value is short)
                        return ctx.MkInt((short)node.Token.Value);
                    if (node.Token.Value is int)
                        return ctx.MkInt((int)node.Token.Value);
                    if (node.Token.Value is long)
                        return ctx.MkInt((long)node.Token.Value);
                    throw new NotImplementedException(node.Kind().ToString());
                case TrueLiteralExpression:
                    return ctx.MkTrue();
                case FalseLiteralExpression:
                    return ctx.MkFalse();
                default:
                    throw new NotImplementedException(node.Kind().ToString());
            }
        }

        internal Expr ToExpr(BinaryExpressionSyntax node)
        {
            var children = node.ChildNodes().ToArray();
            var left = ToExpr(children[0]);
            var right = ToExpr(children[1]);
            try
            {
                switch (node.Kind())
                {
                    case AddExpression:
                        return ctx.MkAdd((ArithExpr)left, (ArithExpr)right) as ArithExpr;
                    case SubtractExpression:
                        return ctx.MkSub((ArithExpr)left, (ArithExpr)right) as ArithExpr;
                    case MultiplyExpression:
                        return ctx.MkMul((ArithExpr)left, (ArithExpr)right) as ArithExpr;
                    case DivideExpression:
                        return ctx.MkDiv((ArithExpr)left, (ArithExpr)right) as ArithExpr;
                    case ModuloExpression:
                        return ctx.MkMod((IntExpr)left, (IntExpr)right) as IntExpr;
                    case GreaterThanExpression:
                        return ctx.MkGt((ArithExpr)left, (ArithExpr)right);
                    case LessThanExpression:
                        return ctx.MkLt((ArithExpr)left, (ArithExpr)right);
                    case GreaterThanOrEqualExpression:
                        return ctx.MkGe((ArithExpr)left, (ArithExpr)right);
                    case LessThanOrEqualExpression:
                        return ctx.MkLe((ArithExpr)left, (ArithExpr)right);
                    case EqualsExpression:
                        return ctx.MkEq(left, right);
                    case NotEqualsExpression:
                        return ctx.MkNot(ctx.MkEq(left, right));
                    case LogicalAndExpression:
                        return ctx.MkAnd(ctx.MkEq(left, right));
                    case LogicalOrExpression:
                        return ctx.MkOr(ctx.MkEq(left, right));
                    default:
                        throw new NotImplementedException(node.Kind().ToString());
                }
            }
            catch (InvalidCastException)
            {
                throw new NotSupportedException(node.GetType().Name + " is not supported.");
            }
        }

        internal Expr ToExpr(MemberAccessExpressionSyntax node)
        {
            var children = node.ChildNodes().ToArray();
            if (children[0] is PredefinedTypeSyntax predefinedType)
            {
                switch (children[1].ToFullString().Trim())
                {
                    case "MaxValue":
                        switch (predefinedType.ToString())
                        {
                            case "byte":
                                return ctx.MkInt(byte.MaxValue);
                            case "short":
                                return ctx.MkInt(short.MaxValue);
                            case "int":
                                return ctx.MkInt(int.MaxValue);
                            case "long":
                                return ctx.MkInt(long.MaxValue);
                            default:
                                throw new NotImplementedException(predefinedType.ToString());
                        }
                    case "MinValue":
                        switch (predefinedType.ToString())
                        {
                            case "byte":
                                return ctx.MkInt(byte.MinValue);
                            case "short":
                                return ctx.MkInt(short.MinValue);
                            case "int":
                                return ctx.MkInt(int.MinValue);
                            case "long":
                                return ctx.MkInt(long.MinValue);
                            default:
                                throw new NotImplementedException(predefinedType.ToString());
                        }
                    default:
                        throw new NotImplementedException(children[1].ToString());
                }
            }
            throw new NotImplementedException(node.GetType().Name);
        }

        private Expr ToExpr(SyntaxToken identifier, TypeSyntax type)
        {
            switch (type.ToString())
            {
                case "bool":
                    return ctx.MkBoolConst(identifier.Text);
                case "byte":
                case "short":
                case "int":
                case "long":
                    return ctx.MkIntConst(identifier.Text);
                case "string[]":
                    return ctx.MkArrayConst(identifier.Text, ctx.StringSort, ctx.StringSort);
                default:
                    throw new NotImplementedException(type.ToString());
            }
        }

        public void Dispose()
        {
            ctx.Dispose();
        }
    }
}
