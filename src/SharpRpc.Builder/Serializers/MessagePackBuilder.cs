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

        private List<TypeString> _messageClassNames = new List<TypeString>();

        public override string Name => "MessagePack";

        public override void BuildUpMessage(MessageBuilder builder)
        {
            _messageClassNames.Add(builder.MessageClassName);

            builder.UpdateClassDeclaration(
                c => c.AddSeparatedAttributes(SH.Attribute(ContractAttributeClassName)));

            for (int i = 0; i < builder.MessageProperties.Count; i++)
            {
                var keyAttr = SH.Attribute(MemberAttributeClassName,
                    SF.AttributeArgument(SH.LiteralExpression(i + 1)));

                builder.UpdatePropertyDeclaration(i, p => p.AddAttributes(keyAttr));
            }
        }

        public override void CompleteMessageBuilding(ref ClassDeclarationSyntax baseMessageClassDeclaration)
        {
            var attrList = new List<AttributeSyntax>();

            for (int i = 0; i < _messageClassNames.Count; i++)
            {
                var msgName = _messageClassNames[i];

                attrList.Add(SH.Attribute(UnionAttributeClassName,
                    SH.AttributeArgument(SH.LiteralExpression(i + 1)),
                    SH.AttributeArgument(SH.TypeOfExpression(msgName.Full))));
            }

            baseMessageClassDeclaration = baseMessageClassDeclaration.
                AddSeparatedAttributes(attrList);
        }

        public override ClassDeclarationSyntax GenerateSerializerAdapter(TypeString serilizerClassName, TypeString baseMessageClassName, GeneratorExecutionContext context)
        {
            return SF.ClassDeclaration(serilizerClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PrivateKeyword))
                .AddBaseListTypes(SF.SimpleBaseType(SF.ParseTypeName(Names.RpcSerializerInterface.Full)))
                .AddMembers(GenerateSerializeMehtod(baseMessageClassName))
                .AddMembers(GenerateDeserializeMehtod(baseMessageClassName));
        }

        private MethodDeclarationSyntax GenerateSerializeMehtod(TypeString baseMessageClassName)
        {
            var messageParam = SH.Parameter("message", Names.MessageInterface.Full);
            var messageWriterParam = SH.Parameter("writer", Names.MessageWriterClass.Full);

            var writerBufferProperty = SH.MemeberOfIdentifier("writer", Names.WriterBufferProperty);
            var messageBaseCast = SF.CastExpression(SF.ParseName(baseMessageClassName.Full), SF.IdentifierName("message"));

            var serilizerCall = SF.InvocationExpression(SH.GenericType(SerializerMethod, baseMessageClassName.Full),
                SH.CallArguments(SF.Argument(writerBufferProperty), SF.Argument(messageBaseCast)));

            return SF.MethodDeclaration(SH.VoidToken(), Names.RpcSerializeMethod)
                .AddParameterListParameters(messageParam, messageWriterParam)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .WithBody(SH.MethodBody(SF.ExpressionStatement(serilizerCall)));
        }

        private MethodDeclarationSyntax GenerateDeserializeMehtod(TypeString baseMessageClassName)
        {
            var messageReaderParam = SH.Parameter("reader", Names.MessageReaderClass.Full);

            var readerBufferProperty = SH.MemeberOfIdentifier("reader", Names.ReaderBufferProperty);

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
