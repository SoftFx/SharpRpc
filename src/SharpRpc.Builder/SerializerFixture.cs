using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using SH = SharpRpc.Builder.SyntaxHelper;

namespace SharpRpc.Builder
{
    public class SerializerFixture
    {
        private List<ClassBuildNode> _roots = new List<ClassBuildNode>();

        public SerializerFixture AddHierachy(ClassBuildNode rootNode)
        {
            _roots.Add(rootNode);
            return this;
        }

        internal IEnumerable<ClassDeclarationSyntax> GenerateSerializationAdapters(ContractDeclaration contract, GeneratorExecutionContext context)
        {
            foreach (var serializerDec in contract.Serializers)
            {
                foreach (var root in _roots)
                    serializerDec.Builder.BuildUpClassHierachy(root);

                yield return serializerDec.Builder.GenerateSerializerAdapter(serializerDec.AdapterClassName, contract.BaseMessageClassName, context);
            }
        }

        public static MethodDeclarationSyntax GenerateSerializerFactory(ContractDeclaration contractInfo)
        {
            StatementSyntax ifRoot = SF.ThrowStatement(
                SF.ObjectCreationExpression(SF.ParseTypeName(Names.RpcConfigurationException.Full))
                .AddArgumentListArguments(SF.Argument(SH.LiteralExpression(""))));

            foreach (var serializer in contractInfo.Serializers)
            {
                var adapterCreationStatement = SF.ReturnStatement(
                    SF.ObjectCreationExpression(SF.ParseTypeName(serializer.AdapterClassName.Short))
                    .AddArgumentListArguments());

                var compExpression = SF.BinaryExpression(SyntaxKind.EqualsExpression,
                    SF.IdentifierName("serializer"),
                    SH.EnumValue(Names.SerializerChoiceEnum.Full, serializer.Builder.EnumVal));

                ifRoot = SF.IfStatement(compExpression, adapterCreationStatement, SF.ElseClause(ifRoot));
            }

            var method = SF.MethodDeclaration(SF.ParseTypeName(Names.RpcSerializerInterface.Full), Names.FacadeSerializerAdapterFactoryMethod)
               .AddModifiers(SF.Token(SyntaxKind.PrivateKeyword), SF.Token(SyntaxKind.StaticKeyword))
               .AddParameterListParameters(SH.Parameter("serializer", Names.SerializerChoiceEnum.Full))
               .WithBody(SF.Block(ifRoot));

            return method;
        }
    }
}
