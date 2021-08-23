﻿// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

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

        public static TypeDeclarationSyntax AddSeparatedAttributes(this TypeDeclarationSyntax classDec, params AttributeSyntax[] attributeDeclarations)
        {
            return AddSeparatedAttributes(classDec, (IEnumerable<AttributeSyntax>)attributeDeclarations);
        }

        public static TypeDeclarationSyntax AddSeparatedAttributes(this TypeDeclarationSyntax classDec, IEnumerable<AttributeSyntax> attributeDeclarations)
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

        public static T GetNamedArgumentOrDefault<T>(this AttributeData attr, string paramName, T defaultVal = default(T))
        {
            foreach (var arg in attr.NamedArguments)
            {
                if (arg.Key == paramName)
                    return (T)arg.Value.Value;
            }

            return defaultVal;
        }

        public static T GetConstructorArgumentOrDefault<T>(this AttributeData attr, int argumentNo, T defaultVal = default(T))
        {
            var args = attr.ConstructorArguments.ToList();

            if (args.Count <= argumentNo)
                return defaultVal;

            return (T)args[argumentNo].Value;
        }

        public static T[] GetConstructorArgumentArray<T>(this AttributeData attr, int argumentNo)
        {
            var args = attr.ConstructorArguments.ToList();

            if (args.Count <= argumentNo)
                return new T[0];

            return args[argumentNo].Values.Select(i => (T)i.Value).ToArray();
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

        public static TypeSyntax GenericType(string typeName, params TypeSyntax[] genericTypeArgs)
        {
            return SyntaxFactory.GenericName(SyntaxFactory.Identifier(typeName),
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList(genericTypeArgs)));
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

        public static SyntaxToken PublicToken()
        {
            return SyntaxFactory.Token(SyntaxKind.PublicKeyword);
        }

        public static SyntaxToken ProtectedToken()
        {
            return SyntaxFactory.Token(SyntaxKind.ProtectedKeyword);
        }

        public static SyntaxToken VirtualToken()
        {
            return SyntaxFactory.Token(SyntaxKind.VirtualKeyword);
        }

        public static SyntaxToken OverrideToken()
        {
            return SyntaxFactory.Token(SyntaxKind.OverrideKeyword);
        }

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

        public static VariableDeclarationSyntax VariableDeclaration(string type, string name, EqualsValueClauseSyntax initializer = null)
        {
            return SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(type))
                .AddVariables(SyntaxFactory.VariableDeclarator(name)
                .WithInitializer(initializer));
                //.AsLocalDeclaration();
        }

        public static LocalDeclarationStatementSyntax LocalVariableDeclaration(string type, string name, EqualsValueClauseSyntax initializer = null)
        {
            return VariableDeclaration(type, name, initializer).AsLocalDeclaration();
        }

        public static VariableDeclarationSyntax VarDeclaration(string name, ExpressionSyntax initializer)
        {
            var varDeclarator = SyntaxFactory.VariableDeclarator(SyntaxFactory.Identifier(name))
                .WithInitializer(SyntaxFactory.EqualsValueClause(initializer));

            return SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"), ToSeparatedList(varDeclarator));
        }

        public static LocalDeclarationStatementSyntax LocalVarDeclaration(string name, ExpressionSyntax initializer)
        {
            return VarDeclaration(name, initializer).AsLocalDeclaration();
        }

        public static FieldDeclarationSyntax FieldDeclaration(string fieldName, TypeSyntax fieldType)
        {
            return SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(fieldType)
                .AddVariables(SyntaxFactory.VariableDeclarator(fieldName)));
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

        public static PropertyDeclarationSyntax AddPrivateAutoSetter(this PropertyDeclarationSyntax propertyDeclaration)
        {
            return propertyDeclaration.AddAccessorListAccessors(
                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
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

        public static ObjectCreationExpressionSyntax AddInitializer(this ObjectCreationExpressionSyntax exp, params ExpressionSyntax[] initNodes)
        {
            return exp.WithInitializer(SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression,
                SyntaxFactory.SeparatedList(initNodes)));
        }

        public static ExpressionSyntax PropertyInitializer(string propName, ExpressionSyntax value)
        {
            return SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.IdentifierName(propName), value);
        }
    }
}
