// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Text;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using SH = SharpRpc.Builder.SyntaxHelper;

namespace SharpRpc.Builder
{
    internal class MessagePackBuilder : SerializerBuilderBase
    {
        public readonly string ContractAttributeClassName = "MessagePack.MessagePackObjectAttribute";
        public readonly string MemberAttributeClassName = "MessagePack.KeyAttribute";
        public readonly string UnionAttributeClassName = "MessagePack.UnionAttribute";
        public readonly string SerializerMethod = "MessagePack.MessagePackSerializer.Serialize";
        public readonly string DeserializerMethod = "MessagePack.MessagePackSerializer.Deserialize";

        public override string Name => "MessagePack";

        public override void BuildUpClassHierachy(ClassBuildNode rootNode)
        {
            ApplyAttributes(rootNode, 0);
        }

        private void ApplyAttributes(ClassBuildNode node, int keySeed)
        {
            node.UpdateDeclaration(
               c => c.AddSeparatedAttributes(SH.Attribute(ContractAttributeClassName)));

            for (int i = 0; i < node.PropertyDeclarations.Count; i++)
            {
                var keyAttr = SH.Attribute(MemberAttributeClassName,
                    SF.AttributeArgument(SH.LiteralExpression(keySeed++)));

                node.UpdatePropertyDeclaration(i, p => p.AddAttributes(keyAttr));
            }

            AddUnions(node);

            foreach (var successor in node.Successors)
                ApplyAttributes(successor, keySeed);
        }

        private void AddUnions(ClassBuildNode node)
        {
            var attrList = new List<AttributeSyntax>();

            for (int i = 0; i < node.Successors.Count; i++)
            {
                var className = node.Successors[i].ClassName;

                attrList.Add(SH.Attribute(UnionAttributeClassName,
                    SH.AttributeArgument(SH.LiteralExpression(i + 1)),
                    SH.AttributeArgument(SH.TypeOfExpression(className.Full))));
            }

            node.UpdateDeclaration(d => d.AddSeparatedAttributes(attrList));
        }

        public override ClassDeclarationSyntax GenerateSerializerAdapter(TypeString serilizerClassName, TypeString baseMessageClassName, GeneratorExecutionContext context)
        {
            var compatibility = new ContractCompatibility(context);

            return SF.ClassDeclaration(serilizerClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PrivateKeyword))
                .AddBaseListTypes(SF.SimpleBaseType(SF.ParseTypeName(Names.RpcSerializerInterface.Full)))
                .AddMembers(GenerateSerializeMehtod(compatibility, baseMessageClassName))
                .AddMembers(GenerateDeserializeMehtod(compatibility, baseMessageClassName));
        }

        private MethodDeclarationSyntax GenerateSerializeMehtod(ContractCompatibility compatibility, TypeString baseMessageClassName)
        {
            var messageParam = SH.Parameter("message", Names.MessageInterface.Full);
            var messageWriterParam = SH.Parameter("writer", Names.MessageWriterClass.Full);

            var writerBufferProperty = SH.MemeberOfIdentifier("writer", compatibility.IsNet5 ? Names.WriterBufferProperty : Names.WriterStreamProperty);
            var messageBaseCast = SF.CastExpression(SF.ParseName(baseMessageClassName.Full), SF.IdentifierName("message"));

            var serilizerCall = SF.InvocationExpression(SH.GenericType(SerializerMethod, baseMessageClassName.Full),
                SH.CallArguments(SF.Argument(writerBufferProperty), SF.Argument(messageBaseCast)));

            return SF.MethodDeclaration(SH.VoidToken(), Names.RpcSerializeMethod)
                .AddParameterListParameters(messageParam, messageWriterParam)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .WithBody(SH.MethodBody(SF.ExpressionStatement(serilizerCall)));
        }

        private MethodDeclarationSyntax GenerateDeserializeMehtod(ContractCompatibility compatibility, TypeString baseMessageClassName)
        {
            var messageReaderParam = SH.Parameter("reader", Names.MessageReaderClass.Full);

            var readerBufferProperty = SH.MemeberOfIdentifier("reader", compatibility.IsNet5 ?  Names.ReaderBufferProperty : Names.ReaderStreamProperty);

            var serilizerCall = SF.InvocationExpression(SH.GenericType(DeserializerMethod, baseMessageClassName.Full),
                SH.CallArguments(SF.Argument(readerBufferProperty)));

            var retStatement = SF.ReturnStatement(serilizerCall);

            return SF.MethodDeclaration(SF.ParseTypeName(Names.MessageInterface.Full), Names.RpcDeserializeMethod)
                .AddParameterListParameters(messageReaderParam)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .WithBody(SH.MethodBody(retStatement));
        }

    }
}
