using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace SharpRpc.Builder
{
    [Generator]
    public class StubGenerator : ISourceGenerator
    {
        private INamedTypeSymbol _contractAttrSymbol;
        private INamedTypeSymbol _rpcAttrSymbol;

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            SyntaxReceiver syntaxReceiver = (SyntaxReceiver)context.SyntaxReceiver;

            var contracts = GetRpcContracts(context);

            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }

            foreach (var contractInfo in contracts)
            {
                new MessageBuilder(contractInfo).GenerateCode(context);
                new ClientStubBuilder(contractInfo).GenerateCode(context);
            }
        }

        private static readonly SymbolDisplayFormat FulluQualifiedSymbolFormat = new SymbolDisplayFormat(
            typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces);

        private IEnumerable<ContractDeclaration> GetRpcContracts(GeneratorExecutionContext context)
        {
            InitSharpRpcTypeSymbols(context);

            SyntaxReceiver syntaxReceiver = (SyntaxReceiver)context.SyntaxReceiver;

            foreach (var interfaceDec in syntaxReceiver.Interfaces)
            {
                var sm = context.Compilation.GetSemanticModel(interfaceDec.SyntaxTree);
                var contractAttr = FindAttribute(interfaceDec, _contractAttrSymbol, sm);

                if (contractAttr != null)
                    yield return CollectContractData(interfaceDec, sm);
            }
        }

        private ISymbol FindAttribute(MemberDeclarationSyntax declaration, INamedTypeSymbol attrTypeSymbol, SemanticModel sm)
        {
            foreach (var attrList in declaration.AttributeLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    var attrSymbolInfo = sm.GetSymbolInfo(attr);

                    if (attrSymbolInfo.Symbol != null && SymbolEqualityComparer.Default.Equals(attrTypeSymbol, attrSymbolInfo.Symbol.ContainingType))
                        return attrSymbolInfo.Symbol;
                }
            }

            return null;
        }

        private AttributeData FindAttribute(ISymbol declaration, INamedTypeSymbol attrTypeSymbol)
        {
            foreach (var attr in declaration.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrTypeSymbol))
                    return attr;
            }

            return null;
        }

        private ContractDeclaration CollectContractData(InterfaceDeclarationSyntax contractTypeDec, SemanticModel sm)
        {
            var contractSmbInfo = sm.GetDeclaredSymbol(contractTypeDec);
            if (contractSmbInfo == null)
                return new ContractDeclaration("Error");
            var fullyQualifiedName = contractSmbInfo.ToDisplayString(FulluQualifiedSymbolFormat);
            var contractInfo = new ContractDeclaration(fullyQualifiedName);

            foreach (var member in contractTypeDec.Members)
            {
                var methodDec = member as MethodDeclarationSyntax;
                if (methodDec != null)
                {
                    var callData = CollectCallData(methodDec, sm);
                    if (callData != null)
                        contractInfo.Calls.Add(callData);
                }
            }

            return contractInfo;
        }

        private CallDeclaration CollectCallData(MethodDeclarationSyntax methodDec, SemanticModel sm)
        {
            var methodModel = (IMethodSymbol)sm.GetDeclaredSymbol(methodDec);
            var callAttr = FindAttribute(methodModel, _rpcAttrSymbol);

            if (callAttr != null)
            {
                var typeArg = callAttr.ConstructorArguments[0];

                if (typeArg.Kind != TypedConstantKind.Enum)
                    throw new Exception("Invalid property type!");

                var callType = GetCallType(typeArg.Value);

                var callInfo = new CallDeclaration(methodModel.Name, callType);

                int index = 1;
                foreach (var paramModel in methodModel.Parameters)
                    callInfo.Params.Add(CollectParamInfo(index++, paramModel));

                if (!methodModel.ReturnsVoid)
                    callInfo.ReturnParam = CollectParamInfo(0, methodModel.ReturnType);

                return callInfo;
            }

            return null;
        }

        private ParamDeclaration CollectParamInfo(int index, IParameterSymbol param)
        {
            return CollectParamInfo(index, param.Type, param.Name);
        }

        private ParamDeclaration CollectParamInfo(int index, ITypeSymbol type, string paramName = null)
        {
            var paramTypeFullName = type.ToDisplayString(FulluQualifiedSymbolFormat);

            return new ParamDeclaration(index, paramTypeFullName, paramName);
        }

        private bool TryGetAttributeValue(AttributeData data, string name, out TypedConstant value)
        {
            foreach (var arg in data.NamedArguments)
            {
                if (arg.Key == name)
                {
                    value = arg.Value;
                    return true;
                }
            }

            value = default(TypedConstant);
            return false;
        }

        private ContractCallType GetCallType(object value)
        {
            switch (value)
            {
                case 0: return ContractCallType.ClientCall;
                case 1: return ContractCallType.ClientMessage;
                case 2: return ContractCallType.ServerCall;
                case 3: return ContractCallType.ServerMessage;
            }

            throw new Exception("Unknonwn call type: " + value);
        }

        private void InitSharpRpcTypeSymbols(GeneratorExecutionContext context)
        {
            _contractAttrSymbol = GetSymbolOrThrow(Names.ContractAttributeClass.Full, context);
            _rpcAttrSymbol = GetSymbolOrThrow(Names.RpcAttributeClass.Full, context);
        }

        private INamedTypeSymbol GetSymbolOrThrow(string metadataName, GeneratorExecutionContext context)
        {
            var symbol = context.Compilation.GetTypeByMetadataName(metadataName);
            if (symbol == null)
                throw new Exception("Cannot find type declaration: " + metadataName);
            return symbol;
        }

        private class SyntaxReceiver : ISyntaxReceiver
        {
            public List<ClassDeclarationSyntax> Classes { get; } = new List<ClassDeclarationSyntax>();
            public List<InterfaceDeclarationSyntax> Interfaces { get; } = new List<InterfaceDeclarationSyntax>();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                var classNode = syntaxNode as ClassDeclarationSyntax;

                if (classNode != null)
                    Classes.Add(classNode);

                var interfaceNode = syntaxNode as InterfaceDeclarationSyntax;

                if (interfaceNode != null)
                    Interfaces.Add(interfaceNode);
            }
        }
    }
}
