using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.Builder
{
    internal class ProtobufNetBuilder : SerializerBuilderBase
    {
        public readonly string ContractAttributeClassName = "ProtoBuf.ProtoContractAttribute";
        public readonly string MemberAttributeClassName = "ProtoBuf.ProtoMemberAttribute";

        public override void BuildMessageSerializer(MessageBuilder builder)
        {
            builder.UpdateClassDeclaration(
                c => c.AddAttributes(SyntaxHelper.Attribute(ContractAttributeClassName)));

            foreach (var param in builder.MessageParams)
            {
                var memberAttr = SyntaxHelper.Attribute(MemberAttributeClassName,
                    SyntaxFactory.AttributeArgument(SyntaxHelper.LiteralExpression(param.Index)));

                builder.UpdatePropertyDeclaration(param.MessagePropertyName,
                    p => p.AddAttributes(memberAttr));
            }
        }
    }
}
