using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.Builder
{
    internal class DataContractBuilder : SerializerBuilderBase
    {
        public readonly string ContractAttributeClassName = "System.Runtime.Serialization.DataContractAttribute";
        public readonly string MemberAttributeClassName = "System.Runtime.Serialization.DataMemberAttribute";

        public override void BuildMessageSerializer(MessageBuilder builder)
        {
            builder.UpdateClassDeclaration(
                c => c.AddAttributes(SyntaxHelper.Attribute(ContractAttributeClassName)));

            foreach (var param in builder.MessageParams)
            {
                var memberAttr = SyntaxHelper.Attribute(MemberAttributeClassName);

                builder.UpdatePropertyDeclaration(param.MessagePropertyName,
                    p => p.AddAttributes(memberAttr));
            }
        }
    }
}
