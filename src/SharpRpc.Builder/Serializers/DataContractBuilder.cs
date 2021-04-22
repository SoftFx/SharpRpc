using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

        public override void BuildUpClassHierachy(ClassBuildNode rootNode)
        {
        }

        //public override void BuildUpMessage(MessageBuilder builder)
        //{
        //    builder.UpdateClassDeclaration(
        //        c => c.AddSeparatedAttributes(SyntaxHelper.Attribute(ContractAttributeClassName)));

        //    for (int i = 0; i < builder.MessageProperties.Count; i++)
        //    {
        //        var memberAttr = SyntaxHelper.Attribute(MemberAttributeClassName);

        //        builder.UpdatePropertyDeclaration(i, p => p.AddAttributes(memberAttr));
        //    }
        //}

        //public override void CompleteMessageBuilding(ref Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax baseMessageClassDeclaration)
        //{

        //}

        public override ClassDeclarationSyntax GenerateSerializerAdapter(TypeString serilizerClassName, TypeString baseMessageClassName, GeneratorExecutionContext context)
        {
            throw new NotImplementedException();
        }
    }
}
