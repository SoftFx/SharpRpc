using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.Builder
{
    public abstract class SerializerBuilderBase
    {
        public abstract string Name { get; }

        public abstract void BuildUpMessage(MessageBuilder builder);
        public abstract void CompleteMessageBuilding(ref ClassDeclarationSyntax baseMessageClassDeclaration);
        public abstract void GenerateSerializerCode(TypeString serilizerClassName, TypeString baseMessageClassName, GeneratorExecutionContext context);
    }
}
