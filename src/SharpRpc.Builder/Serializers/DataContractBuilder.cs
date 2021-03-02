using Microsoft.CodeAnalysis;
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

        public override string Name => "DataContract";

        public override void BuildUpMessage(MessageBuilder builder)
        {
            builder.UpdateClassDeclaration(
                c => c.AddSeparatedAttributes(SyntaxHelper.Attribute(ContractAttributeClassName)));

            foreach (var param in builder.MessageParams)
            {
                var memberAttr = SyntaxHelper.Attribute(MemberAttributeClassName);

                builder.UpdatePropertyDeclaration(param.MessagePropertyName,
                    p => p.AddAttributes(memberAttr));
            }
        }

        public override void CompleteMessageBuilding(ref Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax baseMessageClassDeclaration)
        {
        }

        public override void GenerateSerializerCode(TypeString serilizerClassName, TypeString baseMessageClassName, GeneratorExecutionContext context)
        {
        }
    }
}
