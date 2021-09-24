// Copyright © 2021 Soft-Fx. All rights reserved.
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
using System.Runtime.InteropServices;
using System.Text;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using SH = SharpRpc.Builder.SyntaxHelper;

namespace SharpRpc.Builder
{
    public class SerializerFixture
    {
        private List<ClassBuildNode> _nodes = new List<ClassBuildNode>();

        public SerializerFixture RegisterSerializableClass(ClassBuildNode rootNode)
        {
            _nodes.Add(rootNode);
            return this;
        }

        internal void BuildUpSerializableClasses(ContractDeclaration contract)
        {
            foreach (var serializerDec in contract.Serializers)
                serializerDec.Builder.BuildUpClasses(_nodes);
        }

        internal IEnumerable<ClassDeclarationSyntax> GenerateSerializationAdapters(ContractDeclaration contract, GeneratorExecutionContext context)
        {
            foreach (var serializerDec in contract.Serializers)
                yield return serializerDec.Builder.GenerateSerializerAdapter(serializerDec.AdapterClassName, contract.BaseMessageClassName, context);
        }

        public static MethodDeclarationSyntax GenerateSerializerFactory(ContractDeclaration contractInfo)
        {
            var hasSomeSerializers = contractInfo.Serializers.Count > 0;
            var exceptionMessage = hasSomeSerializers ? "Specified serializer is not supported by this contract." : "No serializer is specified in the contract.";

            StatementSyntax ifRoot = SF.ThrowStatement(
                SF.ObjectCreationExpression(SF.ParseTypeName(Names.RpcConfigurationException.Full))
                .AddArgumentListArguments(SF.Argument(SH.LiteralExpression(exceptionMessage))));

            foreach (var serializer in contractInfo.Serializers)
            {
                var adapterCreationStatement = SF.ReturnStatement(
                    SF.ObjectCreationExpression(SF.ParseTypeName(serializer.AdapterClassName.Short))
                    .AddArgumentListArguments());

                var compExpression = SF.BinaryExpression(SyntaxKind.EqualsExpression,
                    SF.IdentifierName("serializer"),
                    SH.EnumValue(Names.SerializerChoiceEnum.Full, serializer.Builder.EnumVal));

                ifRoot = SF.IfStatement(compExpression, adapterCreationStatement, SF.ElseClause(ifRoot));
            }

            var method = SF.MethodDeclaration(SF.ParseTypeName(Names.RpcSerializerInterface.Full), Names.FacadeSerializerAdapterFactoryMethod)
               .AddModifiers(SF.Token(SyntaxKind.PrivateKeyword), SF.Token(SyntaxKind.StaticKeyword))
               .AddParameterListParameters(SH.Parameter("serializer", Names.SerializerChoiceEnum.Full))
               .WithBody(SF.Block(ifRoot));

            return method;
        }

        public static IEnumerable<ClassDeclarationSyntax> GeneratePrebuildMessages(ContractDeclaration contract)
        {
            bool singleAdapter = contract.Serializers.Count == 1;

            foreach (var callDef in contract.Operations)
            {
                if (callDef.IsOneWay && contract.EnablePrebuild)
                {
                    var msgName = contract.GetPrebuiltMessageClassName(callDef.MethodName);
                    var baseType = singleAdapter ? Names.RpcPrebuiltMessage : Names.RpcMultiPrebuiltMessage;

                    var constructorInitializer = SF.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
                        .AddArgumentListArguments(SH.IdentifierArgument("bytes"));

                    var bytesParam = SH.Parameter("bytes", Names.RpcSegmentedByteArray.Full);

                    var constructor = SF.ConstructorDeclaration(msgName.Short)
                        .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                        .AddParameterListParameters(bytesParam)
                        .WithInitializer(constructorInitializer)
                        .WithBody(SF.Block());

                    yield return SF.ClassDeclaration(msgName.Short)
                        .AddBaseListTypes(SF.SimpleBaseType(SH.FullTypeName(baseType)))
                        .AddModifiers(SH.PublicToken())
                        .AddMembers(constructor);
                }
            }
        }

        public static ClassDeclarationSyntax GeneratePrebuildTool(ContractDeclaration contract)
        {
            bool singleAdapter = contract.Serializers.Count == 1;

            var preserializerField = SH.FieldDeclaration("_preserializer", SH.FullTypeName(Names.RpcPreserializeTool))
                .AddModifiers(SF.Token(SyntaxKind.PrivateKeyword), SF.Token(SyntaxKind.ReadOnlyKeyword));

            var adapters = contract.Serializers.Select(s =>
            {
                var adapterType = SH.ShortTypeName(s.AdapterClassName);
                return SF.Argument(SF.ObjectCreationExpression(adapterType).WithoutArguments());
            });

            var preserializerCreation = SH.AssignmentStatement(
                SF.IdentifierName("_preserializer"),
                SF.ObjectCreationExpression(SH.FullTypeName(Names.RpcPreserializeTool))
                .AddArgumentListArguments(adapters.ToArray()));

            var constructor = SF.ConstructorDeclaration("Prebuilder")
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .WithBody(SF.Block(preserializerCreation));

            var prebuildMethods = new List<MethodDeclarationSyntax>();

            foreach (var callDef in contract.Operations)
            {
                if (callDef.IsOneWay)
                    prebuildMethods.Add(GenPrebuildMethod(callDef, contract, singleAdapter));
            }
            
            return SF.ClassDeclaration("Prebuilder")
                .AddModifiers(SH.PublicToken())
                .AddMembers(preserializerField, constructor)
                .AddMembers(prebuildMethods.ToArray());
        }

        private static MethodDeclarationSyntax GenPrebuildMethod(OperationDeclaration callDef, ContractDeclaration contract, bool singleAdapter)
        {
            var bodyStatements = new List<StatementSyntax>();
            var msgClassName = contract.GetOnWayMessageClassName(callDef);
            var pMessageClassName = contract.GetPrebuiltMessageClassName(callDef.MethodName);

            var pMsgCreationStatement = SF.ObjectCreationExpression(SH.FullTypeName(pMessageClassName))
                .AddArgumentListArguments(SH.IdentifierArgument("bytes"));

            bodyStatements.AddRange(TxStubBuilder.GenerateCreateAndFillMessageStatements(callDef, msgClassName));
            bodyStatements.Add(GenSerializerInvoke(singleAdapter));

            bodyStatements.Add(SF.ReturnStatement(pMsgCreationStatement));

            var methodParams = TxStubBuilder.GenerateMethodParams(callDef);
            return SF.MethodDeclaration(SH.FullTypeName(pMessageClassName), "Prebuild" + callDef.MethodName)
                .AddModifiers(SH.PublicToken())
                .AddParameterListParameters(methodParams.ToArray())
                .WithBody(SF.Block(bodyStatements ));
        }

        private static StatementSyntax GenSerializerInvoke(bool singleAdapter)
        {
            var methodToCall = SH.MemberOfIdentifier("_preserializer",
                singleAdapter ? "SerializeOnSingleAdapter" : "SerializeOnAllAdapters");

            var invokeExpression = SF.InvocationExpression(methodToCall)
                .WithArguments(SH.IdentifierArgument("message"));

            return SH.LocalVarDeclaration("bytes", invokeExpression);
        }
    }
}
