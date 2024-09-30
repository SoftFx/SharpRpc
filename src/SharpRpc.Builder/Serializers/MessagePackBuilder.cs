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
        public readonly string IgnoreMemberAttributeClassName = "MessagePack.IgnoreMemberAttribute";
        public readonly string UnionAttributeClassName = "MessagePack.UnionAttribute";
        public readonly string SerializerMethod = "MessagePack.MessagePackSerializer.Serialize";
        public readonly string DeserializerMethod = "MessagePack.MessagePackSerializer.Deserialize";
        public readonly string SerializerOptionsClassName = "MessagePack.MessagePackSerializerOptions";
        public readonly string SerializerOptionsStandardName = "MessagePack.MessagePackSerializerOptions.Standard";
        public readonly string SerializerOptionsPropertyName = "Options";

        public override string Name => "MessagePack";

        public override void BuildUpClasses(List<ClassBuildNode> classNodes)
        {
            foreach (var node in classNodes)
            {
                AddContractAttribute(node);
                AddMemberAttrubutes(node);

                if (node.Successors.Count > 0)
                    AddUnionAttributes(node);
            }
        }

        private void AddContractAttribute(ClassBuildNode node)
        {
            if (!(node.TypeDeclaration is InterfaceDeclarationSyntax))
            {
                node.UpdateDeclaration(
                   c => c.AddSeparatedAttributes(SH.Attribute(ContractAttributeClassName)));
            }
        }

        private void AddMemberAttrubutes(ClassBuildNode node)
        {
            for (int i = 0; i < node.AuxProperties.Count; i++)
            {
                var ignoreAttr = SH.Attribute(IgnoreMemberAttributeClassName);

                node.UpdateAuxPropertyDeclaration(i, p => p.AddAttributes(ignoreAttr));
            }

            for (int i = 0; i < node.DataProperties.Count; i++)
            {
                var keyAttr = SH.Attribute(MemberAttributeClassName,
                    SF.AttributeArgument(SH.LiteralExpression(i)));

                node.UpdateDataPropertyDeclaration(i, p => p.AddAttributes(keyAttr));
            }
        }

        private void AddUnionAttributes(ClassBuildNode node)
        {
            var attrList = new List<AttributeSyntax>();

            foreach (var successor in node.Successors)
            {
                attrList.Add(SH.Attribute(UnionAttributeClassName,
                    SH.AttributeArgument(SH.LiteralExpression(successor.Key)),
                    SH.AttributeArgument(SH.TypeOfExpression(successor.ClassName.Full))));
            }

            node.UpdateDeclaration(d => d.AddSeparatedAttributes(attrList));
        }

        public override ClassDeclarationSyntax GenerateSerializerAdapter(TypeString serilizerClassName, TypeString baseMessageClassName, GeneratorExecutionContext context)
        {
            var compatibility = new ContractCompatibility(context);

            const string optionsParamName = "options";

            var optionsProperty = SF.PropertyDeclaration(SF.IdentifierName(SerializerOptionsClassName), SerializerOptionsPropertyName)
                .AddModifiers(SH.PublicToken())
                .AddAutoGetter();

            var optionsParam = SH.Parameter(optionsParamName, SerializerOptionsClassName);

            var optionsAssignment = SH.AssignmentStatement(SF.IdentifierName(SerializerOptionsPropertyName), SF.IdentifierName(optionsParamName));

            var parameterConstructor = SF.ConstructorDeclaration(serilizerClassName.Short)
                .AddParameterListParameters(optionsParam)
                .AddModifiers(SH.PublicToken())
                .WithBody(SH.MethodBody(optionsAssignment));

            // Relying on mutable static options is not recommended
            // https://github.com/MessagePack-CSharp/MessagePack-CSharp/blob/master/doc/analyzers/MsgPack001.md
            var defaultConstructorInitializer = SF.ConstructorInitializer(SyntaxKind.ThisConstructorInitializer)
                .AddArgumentListArguments(SH.IdentifierArgument(SerializerOptionsStandardName));

            var defaultConstructor = SF.ConstructorDeclaration(serilizerClassName.Short)
                .AddModifiers(SH.PublicToken())
                .WithInitializer(defaultConstructorInitializer)
                .WithBody(SH.MethodBody());

            return SF.ClassDeclaration(serilizerClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PrivateKeyword))
                .AddBaseListTypes(SF.SimpleBaseType(SF.ParseTypeName(Names.RpcSerializerInterface.Full)))
                .AddMembers(defaultConstructor, parameterConstructor, optionsProperty)
                .AddMembers(GenerateSerializeMehtod(compatibility, baseMessageClassName))
                .AddMembers(GenerateDeserializeMehtod(compatibility, baseMessageClassName));
        }

        private MethodDeclarationSyntax GenerateSerializeMehtod(ContractCompatibility compatibility, TypeString baseMessageClassName)
        {
            var messageParam = SH.Parameter("message", Names.MessageInterface.Full);
            var messageWriterParam = SH.Parameter("writer", Names.MessageWriterClass.Full);

            var writerBufferProperty = SH.MemberOfIdentifier("writer", compatibility.IsNet5 ? Names.WriterBufferProperty : Names.WriterStreamProperty);
            var messageBaseCast = SF.CastExpression(SF.ParseName(baseMessageClassName.Full), SF.IdentifierName("message"));

            var serilizerCall = SF.InvocationExpression(SH.GenericType(SerializerMethod, baseMessageClassName.Full),
                SH.CallArguments(SF.Argument(writerBufferProperty), SF.Argument(messageBaseCast), SH.IdentifierArgument(SerializerOptionsPropertyName)));

            return SF.MethodDeclaration(SH.VoidToken(), Names.RpcSerializeMethod)
                .AddParameterListParameters(messageParam, messageWriterParam)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .WithBody(SH.MethodBody(SF.ExpressionStatement(serilizerCall)));
        }

        private MethodDeclarationSyntax GenerateDeserializeMehtod(ContractCompatibility compatibility, TypeString baseMessageClassName)
        {
            var messageReaderParam = SH.Parameter("reader", Names.MessageReaderClass.Full);

            var readerBufferProperty = SH.MemberOfIdentifier("reader", compatibility.IsNet5 ?  Names.ReaderBufferProperty : Names.ReaderStreamProperty);

            var serilizerCall = SF.InvocationExpression(SH.GenericType(DeserializerMethod, baseMessageClassName.Full),
                SH.CallArguments(SF.Argument(readerBufferProperty), SH.IdentifierArgument(SerializerOptionsPropertyName)));

            var retStatement = SF.ReturnStatement(serilizerCall);

            return SF.MethodDeclaration(SF.ParseTypeName(Names.MessageInterface.Full), Names.RpcDeserializeMethod)
                .AddParameterListParameters(messageReaderParam)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .WithBody(SH.MethodBody(retStatement));
        }

        internal override MethodDeclarationSyntax GenerateCreateClientFactoryMethod(TypeString serilizerClassName, TxStubBuilder clientBuilder)
        {
            var serializerParam = SH.Parameter("options", SerializerOptionsClassName);

            var serializerAdapterCreateClause = SF.ObjectCreationExpression(SF.IdentifierName(serilizerClassName.Short))
                .AddArgumentListArguments(SH.IdentifierArgument("options"));
            var serializerAdapterVarStatement = SH.LocalVarDeclaration("serializerAdapter", serializerAdapterCreateClause);

            return clientBuilder.GenerateFactoryMethodPublic(serializerParam, serializerAdapterVarStatement, SH.IdentifierArgument("serializerAdapter"));
        }

        internal override MethodDeclarationSyntax GenerateCreateServerFactoryMethod(TypeString serilizerClassName, RxStubBuilder serverBuilder)
        {
            var serializerParam = SH.Parameter("options", SerializerOptionsClassName);

            var serializerAdapterCreateClause = SF.ObjectCreationExpression(SF.IdentifierName(serilizerClassName.Short))
                .AddArgumentListArguments(SH.IdentifierArgument("options"));
            var serializerAdapterVarStatement = SH.LocalVarDeclaration("serializerAdapter", serializerAdapterCreateClause);

            return serverBuilder.GenerateServiceDescriptorFactoryPublic(serializerParam, serializerAdapterVarStatement, SH.IdentifierArgument("serializerAdapter"));
        }
    }
}
