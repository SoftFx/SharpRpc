using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.Builder
{
    internal class MessagePackBuilder : SerializerBuilderBase
    {
        public readonly string ContractAttributeClassName = "MessagePack.MessagePackObjectAttribute";
        public readonly string MemberAttributeClassName = "MessagePack.KeyAttribute";

        public override void BuildMessageSerializer(MessageBuilder builder)
        {
            builder.UpdateClassDeclaration(
                c => c.AddAttributes(SyntaxHelper.Attribute(ContractAttributeClassName)));

            foreach (var param in builder.MessageParams)
            {
                var keyAttr = SyntaxHelper.Attribute(MemberAttributeClassName,
                    SyntaxFactory.AttributeArgument(SyntaxHelper.LiteralExpression(param.Index)));

                builder.UpdatePropertyDeclaration(param.MessagePropertyName,
                    p => p.AddAttributes(keyAttr));
            }
        }
    }
}
