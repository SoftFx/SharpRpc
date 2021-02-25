using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpRpc.Builder
{
    internal static class SyntaxHelper
    {
        public static AttributeSyntax Attribute(string typeName, params AttributeArgumentSyntax[] args)
        {
            return SyntaxFactory.Attribute(
                    SyntaxFactory.ParseName(typeName),
                    SyntaxFactory.AttributeArgumentList(
                        SyntaxFactory.SeparatedList(args)));
        }

        public static AttributeArgumentSyntax AttributeArgument(ExpressionSyntax value)
        {
            return SyntaxFactory.AttributeArgument(value);
        }

        public static ClassDeclarationSyntax AddAttributes(this ClassDeclarationSyntax classDec, params AttributeSyntax[] attributeDeclarations)
        {
            return classDec.AddAttributeLists(
                SyntaxFactory.AttributeList(
                    SyntaxFactory.SeparatedList<AttributeSyntax>(attributeDeclarations)));
        }

        public static PropertyDeclarationSyntax AddAttributes(this PropertyDeclarationSyntax propDec, params AttributeSyntax[] attributeDeclarations)
        {
            return propDec.AddAttributeLists(
                SyntaxFactory.AttributeList(
                    SyntaxFactory.SeparatedList<AttributeSyntax>(attributeDeclarations)));
        }

       
        public static ParameterSyntax Parameter(string paramName, string paramType)
        {
            return SyntaxFactory.Parameter(SyntaxFactory.Identifier(paramName))
                .WithType(SyntaxFactory.ParseTypeName(paramType));
        }

        public static ArgumentSyntax IdentifierArgument(string name)
        {
            return SyntaxFactory.Argument(SyntaxFactory.IdentifierName(name));
        }

        public static ArgumentListSyntax CallArguments(params ArgumentSyntax[] arguments)
        {
            return SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList<ArgumentSyntax>(arguments));
        }

        public static ArgumentListSyntax CallArguments(IEnumerable<ArgumentSyntax> arguments)
        {
            return SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList<ArgumentSyntax>(arguments));
        }

        public static LocalDeclarationStatementSyntax VariableDeclaration(string type, string name, EqualsValueClauseSyntax initializer = null)
        {
            return SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(type))
                .AddVariables(SyntaxFactory.VariableDeclarator(name)
                .WithInitializer(initializer))
                .AsLocalDeclaration();
        }

        public static ExpressionStatementSyntax AssignmentStatement(ExpressionSyntax left, ExpressionSyntax right)
        {
            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, left, right));
        }

        public static InvocationExpressionSyntax ThisInvocation(string methodName, params ArgumentSyntax[] arguments)
        {
            return SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(methodName), CallArguments(arguments));
        }

        public static StatementSyntax ThisCallStatement(string methodName, params ArgumentSyntax[] arguments)
        {
            return SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(methodName), CallArguments(arguments)));
        }

        public static MemberAccessExpressionSyntax VarPropertyAccess(string variableName, string propertyName)
        {
            return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("message"), SyntaxFactory.IdentifierName(propertyName));
        }

        public static LocalDeclarationStatementSyntax AsLocalDeclaration(this VariableDeclarationSyntax variableDeclaration)
        {
            return SyntaxFactory.LocalDeclarationStatement(variableDeclaration);
        }

        public static BlockSyntax MethodBody(params StatementSyntax[] statements)
        {
            return SyntaxFactory.Block(statements);
        }

        public static BlockSyntax MethodBody(IEnumerable<StatementSyntax> statements)
        {
            return SyntaxFactory.Block(statements);
        }

        public static PropertyDeclarationSyntax AddAutoGetter(this PropertyDeclarationSyntax propertyDeclaration)
        {
            return propertyDeclaration.AddAccessorListAccessors(
                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
        }

        public static PropertyDeclarationSyntax AddAutoSetter(this PropertyDeclarationSyntax propertyDeclaration)
        {
            return propertyDeclaration.AddAccessorListAccessors(
                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
        }

        public static TypeSyntax GenericType(string typeName, params string[] genericTypeArgs)
        {
            return SyntaxFactory.GenericName(SyntaxFactory.Identifier(typeName),
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList(
                        genericTypeArgs.Select(a => SyntaxFactory.ParseTypeName(a)))));
        }

        public static MethodDeclarationSyntax WithoutBody(this MethodDeclarationSyntax method)
        {
            return method.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }

        public static LiteralExpressionSyntax LiteralExpression(int number)
        {
            return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(number));
        }
    }
}
