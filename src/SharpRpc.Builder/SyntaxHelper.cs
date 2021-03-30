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
        #region Attributes

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

        public static ClassDeclarationSyntax AddSeparatedAttributes(this ClassDeclarationSyntax classDec, params AttributeSyntax[] attributeDeclarations)
        {
            return AddSeparatedAttributes(classDec, (IEnumerable<AttributeSyntax>)attributeDeclarations);
        }

        public static ClassDeclarationSyntax AddSeparatedAttributes(this ClassDeclarationSyntax classDec, IEnumerable<AttributeSyntax> attributeDeclarations)
        {
            return classDec.AddAttributeLists(
                attributeDeclarations.Select(d => SyntaxFactory.AttributeList(ToSeparatedList(d))).ToArray());
        }

        private static SeparatedSyntaxList<T> ToSeparatedList<T>(T singleVal)
            where T : SyntaxNode
        {
            return SyntaxFactory.SingletonSeparatedList<T>(singleVal);
        }

        public static PropertyDeclarationSyntax AddAttributes(this PropertyDeclarationSyntax propDec, params AttributeSyntax[] attributeDeclarations)
        {
            return propDec.AddAttributeLists(
                SyntaxFactory.AttributeList(
                    SyntaxFactory.SeparatedList<AttributeSyntax>(attributeDeclarations)));
        }

        #endregion

        #region Types

        public static TypeSyntax ShortTypeName(TypeString type)
        {
            return SyntaxFactory.ParseTypeName(type.Short);
        }

        public static TypeSyntax FullTypeName(TypeString type)
        {
            return SyntaxFactory.ParseTypeName(type.Full);
        }

        public static QualifiedNameSyntax GlobalTypeName(TypeString type)
        {
            var fullNamespace = SyntaxFactory.AliasQualifiedName(
                SyntaxFactory.IdentifierName(SyntaxFactory.Token(SyntaxKind.GlobalKeyword)),
                SyntaxFactory.IdentifierName(type.Namespace));

            return SyntaxFactory.QualifiedName(fullNamespace, SyntaxFactory.IdentifierName(type.Short));
        }

        public static TypeSyntax GenericType(string typeName, params string[] genericTypeArgs)
        {
            return SyntaxFactory.GenericName(SyntaxFactory.Identifier(typeName),
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList(
                        genericTypeArgs.Select(a => SyntaxFactory.ParseTypeName(a)))));
        }

        #endregion

        #region Names

        public static GenericNameSyntax GenericName(string baseName, params string[] genericTypeArgs)
        {
            return SyntaxFactory.GenericName(SyntaxFactory.Identifier(baseName),
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList(
                        genericTypeArgs.Select(a => SyntaxFactory.ParseTypeName(a)))));
        }

        #endregion

        public static PredefinedTypeSyntax VoidToken()
        {
            return SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword));
        }

        public static ParameterSyntax Parameter(string paramName, string paramType)
        {
            return SyntaxFactory.Parameter(SyntaxFactory.Identifier(paramName))
                .WithType(SyntaxFactory.ParseTypeName(paramType));
        }

        public static ParameterSyntax Parameter(string paramName, TypeSyntax paramType)
        {
            return SyntaxFactory.Parameter(SyntaxFactory.Identifier(paramName))
                .WithType(paramType);
        }

        #region Arguments

        public static ArgumentSyntax IdentifierArgument(string name)
        {
            return SyntaxFactory.Argument(SyntaxFactory.IdentifierName(name));
        }

        public static InvocationExpressionSyntax WithArguments(this InvocationExpressionSyntax invoke, params ArgumentSyntax[] arguments)
        {
            return invoke.WithArgumentList(CallArguments(arguments));
        }

        public static ObjectCreationExpressionSyntax WithoutArguments(this ObjectCreationExpressionSyntax objCreation)
        {
            return objCreation.AddArgumentListArguments();
        }

        public static ArgumentListSyntax CallArguments(params ArgumentSyntax[] arguments)
        {
            return SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList<ArgumentSyntax>(arguments));
        }

        public static ArgumentListSyntax CallArguments(IEnumerable<ArgumentSyntax> arguments)
        {
            return SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList<ArgumentSyntax>(arguments));
        }

        #endregion

        public static LocalDeclarationStatementSyntax VariableDeclaration(string type, string name, EqualsValueClauseSyntax initializer = null)
        {
            return SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(type))
                .AddVariables(SyntaxFactory.VariableDeclarator(name)
                .WithInitializer(initializer))
                .AsLocalDeclaration();
        }

        public static LocalDeclarationStatementSyntax VarDeclaration(string name, ExpressionSyntax initializer)
        {
            var varDeclarator = SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(name))
                .WithInitializer(SyntaxFactory.EqualsValueClause(initializer));

            return SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"), ToSeparatedList(varDeclarator))
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

        public static InvocationExpressionSyntax InvocationExpression(string methodName, params ArgumentSyntax[] arguments)
        {
            return SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(methodName), CallArguments(arguments));
        }

        public static MemberAccessExpressionSyntax MemeberOfIdentifier(string variableName, string memberName)
        {
            return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(variableName), SyntaxFactory.IdentifierName(memberName));
        }

        public static MemberAccessExpressionSyntax MemberOf(ExpressionSyntax expression, string memberName)
        {
            return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                expression, SyntaxFactory.IdentifierName(memberName));
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

        public static MethodDeclarationSyntax WithoutBody(this MethodDeclarationSyntax method)
        {
            return method.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }

        public static ExpressionSyntax TypeOfExpression(string typeName)
        {
            return SyntaxFactory.TypeOfExpression(SyntaxFactory.ParseName(typeName));
        }

        public static LiteralExpressionSyntax LiteralExpression(int number)
        {
            return SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(number));
        }

        public static LiteralExpressionSyntax LiteralExpression(string str)
        {
            return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(str));
        }

        public static MemberAccessExpressionSyntax EnumValue(string enumType, string valueName)
        {
            return SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.ParseTypeName(enumType),
                                    SyntaxFactory.IdentifierName(valueName));
        }
    }
}
