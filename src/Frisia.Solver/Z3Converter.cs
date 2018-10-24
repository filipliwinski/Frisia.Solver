﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Z3;
using System;
using System.Linq;
using SK = Microsoft.CodeAnalysis.CSharp.SyntaxKind;

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

        internal Expr ToExpr(ExpressionSyntax expression)
        {
            var children = expression.ChildNodes().ToArray();

            if (children.Length == 0)
            {
                switch (expression.Kind())
                {
                    case SK.IdentifierName:
                        return (BoolExpr)ToExpr(expression);
                    case SK.NumericLiteralExpression:
                        var literalExpression = (LiteralExpressionSyntax)expression;
                        return ctx.MkInt((int)literalExpression.Token.Value);
                    default:
                        throw new NotImplementedException(expression.Kind().ToString());
                }
            }
            else
            {
                var left = ToExpr(children[0]);
                if (children.Length == 1)
                {
                    switch (expression.Kind())
                    {
                        case SK.LogicalNotExpression:
                            return ctx.MkNot((BoolExpr)left);
                        default:
                            throw new NotImplementedException(expression.Kind().ToString());
                    }
                }
                else
                {
                    if (expression.IsKind(SK.InvocationExpression))
                    {
                        throw new NotSupportedException("InvocationExpression is not supported.");
                    }

                    var right = ToExpr(children[1]);

                    try
                    {
                        switch (expression.Kind())
                        {
                            case SK.GreaterThanExpression:
                                return ctx.MkGt((ArithExpr)left, (ArithExpr)right);
                            case SK.LessThanExpression:
                                return ctx.MkLt((ArithExpr)left, (ArithExpr)right);
                            case SK.GreaterThanOrEqualExpression:
                                return ctx.MkGe((ArithExpr)left, (ArithExpr)right);
                            case SK.LessThanOrEqualExpression:
                                return ctx.MkLe((ArithExpr)left, (ArithExpr)right);
                            case SK.EqualsExpression:
                                return ctx.MkEq(left, right);
                            case SK.NotEqualsExpression:
                                return ctx.MkNot(ctx.MkEq(left, right));
                            default:
                                throw new NotImplementedException(expression.Kind().ToString());
                        }
                    }
                    catch (InvalidCastException)
                    {
                        throw new NotSupportedException("Given expression is not supported.");
                    }
                }
            }
        }

        internal Expr ToExpr(SyntaxNode node)
        {
            var nodeType = node.GetType();
            if (nodeType == typeof(IdentifierNameSyntax))
            {
                // Get type of identifier
                var identifierName = (IdentifierNameSyntax)node;
                try
                {
                    var parameter = parameters.Single(x => x.Identifier.ValueText == identifierName.Identifier.ValueText);

                    return ToExpr(identifierName.Identifier, parameter.Type);
                }
                catch (InvalidOperationException)
                {
                    throw new NotSupportedException(identifierName.Identifier.Text + " is not supported.");
                }
            }
            if (nodeType == typeof(LiteralExpressionSyntax))
            {
                var literalExpression = (LiteralExpressionSyntax)node;
                switch (literalExpression.Kind())
                {
                    case SK.NumericLiteralExpression:
                        return ctx.MkInt((int)literalExpression.Token.Value);
                    case SK.TrueLiteralExpression:
                        return ctx.MkTrue();
                    case SK.FalseLiteralExpression:
                        return ctx.MkFalse();
                    default:
                        throw new NotImplementedException(literalExpression.Kind().ToString());
                }
            }
            if (nodeType == typeof(ArrayTypeSyntax))
            {
                var array = (ArrayTypeSyntax)node;

                switch (array.ElementType.ToString())
                {
                    case "string":
                        return ctx.MkConstArray(ctx.StringSort, ctx.MkString(""));
                    default:
                        throw new NotImplementedException(array.ElementType + "[]");
                }
            }
            if (nodeType == typeof(BinaryExpressionSyntax))
            {
                var binaryExpression = (BinaryExpressionSyntax)node;
                var children = binaryExpression.ChildNodes().ToArray();
                var left = ToExpr(children[0]);
                var right = ToExpr(children[1]);
                try
                {
                    switch (binaryExpression.Kind())
                    {
                        case SK.AddExpression:
                            return ctx.MkAdd((ArithExpr)left, (ArithExpr)right) as ArithExpr;
                        case SK.SubtractExpression:
                            return ctx.MkSub((ArithExpr)left, (ArithExpr)right) as ArithExpr;
                        case SK.MultiplyExpression:
                            return ctx.MkMul((ArithExpr)left, (ArithExpr)right) as ArithExpr;
                        case SK.DivideExpression:
                            return ctx.MkDiv((ArithExpr)left, (ArithExpr)right) as ArithExpr;
                        case SK.ModuloExpression:
                            return ctx.MkMod((IntExpr)left, (IntExpr)right) as IntExpr;
                        case SK.GreaterThanExpression:
                            return ctx.MkGt((ArithExpr)left, (ArithExpr)right);
                        case SK.LessThanExpression:
                            return ctx.MkLt((ArithExpr)left, (ArithExpr)right);
                        case SK.GreaterThanOrEqualExpression:
                            return ctx.MkGe((ArithExpr)left, (ArithExpr)right);
                        case SK.LessThanOrEqualExpression:
                            return ctx.MkLe((ArithExpr)left, (ArithExpr)right);
                        case SK.EqualsExpression:
                            return ctx.MkEq(left, right);
                        case SK.NotEqualsExpression:
                            return ctx.MkNot(ctx.MkEq(left, right));
                        case SK.LogicalAndExpression:
                            return ctx.MkAnd(ctx.MkEq(left, right));
                        case SK.LogicalOrExpression:
                            return ctx.MkOr(ctx.MkEq(left, right));
                        default:
                            throw new NotImplementedException(binaryExpression.Kind().ToString());
                    }
                }
                catch (InvalidCastException)
                {
                    throw new NotSupportedException(node.GetType().Name + " is not supported.");
                }
            }
            if (nodeType == typeof(ParenthesizedExpressionSyntax))
            {
                var parenthesizedExpression = (ParenthesizedExpressionSyntax)node;
                return ToExpr(parenthesizedExpression.ChildNodes().Single());
            }
            if (nodeType == typeof(InvocationExpressionSyntax))
            {
                var invocationExpression = (InvocationExpressionSyntax)node;
                
                throw new NotSupportedException(node.GetType().Name + " is not supported.");
            }
            if (nodeType == typeof(MemberAccessExpressionSyntax))
            {
                var memberAccessExpression = (MemberAccessExpressionSyntax)node;
                var children = memberAccessExpression.ChildNodes().ToArray();
                if (children[0].GetType() == typeof(PredefinedTypeSyntax))
                {
                    var predefinedType = (PredefinedTypeSyntax)children[0];
                    switch (children[1].ToFullString().Trim())
                    {
                        case "MaxValue":
                            switch (predefinedType.ToString())
                            {
                                case "int":
                                    return ctx.MkInt(int.MaxValue);
                                default:
                                    break;
                            }
                            break;
                        case "MinValue":
                            switch (predefinedType.ToString())
                            {
                                case "int":
                                    return ctx.MkInt(int.MinValue);
                                default:
                                    break;
                            }
                            break;
                        default:
                            throw new NotImplementedException(children[1].ToString());
                    }
                }
                throw new NotImplementedException(node.GetType().Name);
            }
            if (nodeType == typeof(CastExpressionSyntax))
            {
                var castExpression = (CastExpressionSyntax)node;
                if (castExpression.Expression.GetType() == typeof(IdentifierNameSyntax))
                {
                    var identifierName = (IdentifierNameSyntax)castExpression.Expression;
                    return ToExpr(identifierName.Identifier, castExpression.Type);
                }
                if (castExpression.Expression.GetType() == typeof(BinaryExpressionSyntax))
                {
                    var binaryExpression = (BinaryExpressionSyntax)castExpression.Expression;
                    return ToExpr(binaryExpression);
                }
                throw new NotImplementedException(node.GetType().Name);
            }
            if (nodeType == typeof(PrefixUnaryExpressionSyntax))
            {
                var prefixUnaryExpression = (PrefixUnaryExpressionSyntax)node;

                var expression = ToExpr(prefixUnaryExpression.Operand);
                return ctx.MkUnaryMinus((ArithExpr)expression);
            }
            throw new NotImplementedException(node.GetType().Name);
        }

        private Expr ToExpr(SyntaxToken identifier, TypeSyntax type)
        {
            switch (type.ToString())
            {
                case "bool":
                    return ctx.MkBoolConst(identifier.Text);
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