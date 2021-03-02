using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.Builder
{
    internal class ProtobufNetBuilder : SerializerBuilderBase
    {
        public readonly string ContractAttributeClassName = "ProtoBuf.ProtoContractAttribute";
        public readonly string MemberAttributeClassName = "ProtoBuf.ProtoMemberAttribute";

        public override string Name => "ProtobufNet";

        public override void BuildUpMessage(MessageBuilder builder)
        {
            builder.UpdateClassDeclaration(
                c => c.AddSeparatedAttributes(SyntaxHelper.Attribute(ContractAttributeClassName)));

            foreach (var param in builder.MessageParams)
            {
                var memberAttr = SyntaxHelper.Attribute(MemberAttributeClassName,
                    SyntaxFactory.AttributeArgument(SyntaxHelper.LiteralExpression(param.Index)));

                builder.UpdatePropertyDeclaration(param.MessagePropertyName,
                    p => p.AddAttributes(memberAttr));
            }
        }

        public override void CompleteMessageBuilding(ref ClassDeclarationSyntax baseMessageClassDeclaration)
        {
        }

        public override void GenerateSerializerCode(TypeString serilizerClassName, TypeString baseMessageClassName, GeneratorExecutionContext context)
        {
        }
    }
}
