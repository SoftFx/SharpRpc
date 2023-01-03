// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using SharpRpc.Builder.Metadata;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace SharpRpc.Builder
{
    [Generator]
    public class SharpRpcGenerator : ISourceGenerator
    {
        private INamedTypeSymbol _contractAttrSymbol;
        private INamedTypeSymbol _rpcAttrSymbol;
        private INamedTypeSymbol _serializerAttrSymbol;
        private INamedTypeSymbol _faultContractAttribute;
        private INamedTypeSymbol _inputStreamAttribute;
        private INamedTypeSymbol _outputStreamAttribute;

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            try
            {
#if DEBUG_BUILDER
                if (!Debugger.IsAttached)
                {
                    Debugger.Launch();
                }
#endif
                var diagnostics = new MetadataDiagnostics();

                // collect contracts

                var contracts = GetRpcContracts(context).ToList();

                // validate contracts

                foreach (var contractInfo in contracts)
                    contractInfo.Validate(diagnostics);

                // generate stubs

                foreach (var contractInfo in contracts)
                    GenerateStubFile(contractInfo, context, diagnostics);

                // report errors

                diagnostics.DumpRecordsTo(context);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Exception in StubGenerator.Execute(): " + ex.Message);
                throw;
            }
        }

        private void GenerateStubFile(ContractDeclaration contractInfo, GeneratorExecutionContext context, MetadataDiagnostics diagnostics)
        {
            var contractGenClass = GenerateStubBundle(contractInfo, context, diagnostics);

            var stubNamespace = SF.NamespaceDeclaration(SF.IdentifierName(contractInfo.Namespace))
                    .AddMembers(contractGenClass);

            var compUnit = SF.CompilationUnit()
                .AddMembers(stubNamespace);

            var srcCode = compUnit
                .NormalizeWhitespace()
                .ToFullString();

            context.AddSource(contractInfo.FacadeClassName.Full, SourceText.From(srcCode, Encoding.UTF8));
        }

        private ClassDeclarationSyntax GenerateStubBundle(ContractDeclaration contractInfo, GeneratorExecutionContext context, MetadataDiagnostics diagnostics)
        {
            var contractGenClassName = contractInfo.FacadeClassName;
            var hasPrebuilder = contractInfo.EnablePrebuild && contractInfo.Operations.Any(c => c.IsOneWay);
            var hasCallbacks = contractInfo.Operations.Any(c => c.IsCallback);

            var clientBuilder = new TxStubBuilder(contractInfo, false);
            var serverBuilder = new RxStubBuilder(contractInfo, false);

            var sFixture = new SerializerFixture();

            var messageBundleNode = MessageBuilder.GenerateMessageBundle(contractInfo, sFixture, diagnostics);

            sFixture.BuildUpSerializableClasses(contractInfo);

            var messageBuindleClass = messageBundleNode.CompleteBuilding();

            var sAdapterClasses = sFixture
                .GenerateSerializationAdapters(contractInfo, context)
                .ToArray();

            var clientFactoryMethod = clientBuilder.GenerateFactoryMethod();
            var sAdapterFactoryMethod = SerializerFixture.GenerateSerializerFactory(contractInfo);
            var descriptorFactoryMethod = GenerateDescriptorFactoryMethod(contractInfo);
            var serviceFactoryMethod = serverBuilder.GenerateBindMethod();

            var contractGenClass = SF.ClassDeclaration(contractGenClassName.Short)
               .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
               .AddMembers(clientFactoryMethod, serviceFactoryMethod, sAdapterFactoryMethod, descriptorFactoryMethod)
               .AddMembers(clientBuilder.GenerateCode(diagnostics), serverBuilder.GenerateServiceBase(), serverBuilder.GenerateHandler())
               .AddMembers(sAdapterClasses)
               .AddMembers(messageBuindleClass);

            if (hasCallbacks)
            {
                var callbackClientBuilder = new TxStubBuilder(contractInfo, true);
                var callbackServiceBuilder = new RxStubBuilder(contractInfo, true);
                var callbackClientClass = callbackClientBuilder.GenerateCode(diagnostics);
                var callbackServiceClass = callbackServiceBuilder.GenerateServiceBase();
                var handlerClass = callbackServiceBuilder.GenerateHandler();

                contractGenClass = contractGenClass.AddMembers(callbackClientClass, callbackServiceClass, handlerClass);
            }

            if (hasPrebuilder)
            {
                var prebuiltMessages = SerializerFixture.GeneratePrebuildMessages(contractInfo).ToArray();

                var prebuiltMessageBundleClass = SF.ClassDeclaration(contractInfo.PrebuiltBundleClassName.Short)
                    .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                    .AddMembers(prebuiltMessages);

                var prebuildClass = SerializerFixture.GeneratePrebuildTool(contractInfo);
                contractGenClass = contractGenClass.AddMembers(prebuildClass, prebuiltMessageBundleClass);
            }

            return contractGenClass;
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
                var contractAttr = FindAttribute(sm.GetDeclaredSymbol(interfaceDec), _contractAttrSymbol);

                if (contractAttr != null)
                {
                    var contract = CollectContractData(interfaceDec, contractAttr, context, sm);
                    if (contract != null)
                        yield return contract;
                }
            }
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

        private IEnumerable<AttributeData> FindAttributes(ISymbol declaration, INamedTypeSymbol attrTypeSymbol)
        {
            foreach (var attr in declaration.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrTypeSymbol))
                    yield return attr;
            }
        }

        private IEnumerable<AttributeData> FindAllAttributes(ISymbol declaration, INamedTypeSymbol attrTypeSymbol)
        {
            foreach (var attr in declaration.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrTypeSymbol))
                    yield return attr;
            }
        }

        private ContractDeclaration CollectContractData(InterfaceDeclarationSyntax contractTypeDec, AttributeData contractAttr, GeneratorExecutionContext context, SemanticModel sm)
        {
            bool hasErrors = false;

            var contractSmbInfo = sm.GetDeclaredSymbol(contractTypeDec);
            var compatibilityAdapter = new ContractCompatibility(context);
            if (contractSmbInfo == null)
                return new ContractDeclaration("Error", compatibilityAdapter);
            var fullyQualifiedName = contractSmbInfo.ToDisplayString(FulluQualifiedSymbolFormat);
            var contractInfo = new ContractDeclaration(fullyQualifiedName, compatibilityAdapter);

            contractInfo.EnablePrebuild = contractAttr.GetNamedArgumentOrDefault<bool>(Names.PrebuildCallOption);
            contractInfo.EnablePostResponseMethods = contractAttr.GetNamedArgumentOrDefault<bool>(Names.PostResponseOption);

            foreach (var builder in CollectSerializersData(contractSmbInfo))
                contractInfo.AddSerializer(builder);

            foreach (var member in contractTypeDec.Members)
            {
                try
                {
                    var methodDec = member as MethodDeclarationSyntax;
                    if (methodDec != null)
                    {
                        var callData = CollectCallData(methodDec, sm, contractInfo);
                        if (callData != null)
                            contractInfo.Operations.Add(callData);
                    }
                }
                catch (MetadataException mex)
                {
                    context.ReportDiagnostic(mex.ErrorInfo);
                    hasErrors = true;
                }
            }

            return hasErrors ? null : contractInfo;
        }

        private List<SerializerBuilderBase> CollectSerializersData(ISymbol contractInterfaceModel)
        {
            var result = new List<SerializerBuilderBase>();
            var serializerAttributes = FindAllAttributes(contractInterfaceModel, _serializerAttrSymbol).ToList();

            foreach (var attr in serializerAttributes)
            {
                var typeArg = attr.ConstructorArguments[0];

                if (typeArg.Kind != TypedConstantKind.Enum)
                    throw new Exception("Invalid property type!");

                switch (typeArg.Value)
                {
                    case 0: result.Add(new DataContractBuilder() { EnumVal = "DataContract" }); break;
                    case 1: result.Add(new MessagePackBuilder() { EnumVal = "MessagePack" }); break;
                    case 2: result.Add(new ProtobufNetBuilder() { EnumVal = "ProtobufNet" }); break;
                    default: throw new Exception("Unknown serializer type! This may indicate that your SharpRpc.Builder.dll is outdated!");
                }
            }

            return result;
        }

        private OperationDeclaration CollectCallData(MethodDeclarationSyntax methodDec, SemanticModel sm, ContractDeclaration contract)
        {
            var methodModel = sm.GetDeclaredSymbol(methodDec);
            var callAttr = FindAttribute(methodModel, _rpcAttrSymbol);

            if (callAttr != null)
            {
                var key = GetCallKey(callAttr.ConstructorArguments[0]);
                var callType = GetCallType(callAttr.ConstructorArguments[1]);
                
                var callInfo = new OperationDeclaration(key, methodModel.Name, methodModel.Locations[0], callType);

                int index = 1;
                foreach (var paramModel in methodModel.Parameters)
                    callInfo.Params.Add(CollectParamInfo(index++, paramModel));

                if (!methodModel.ReturnsVoid)
                    callInfo.ReturnParam = CollectParamInfo(0, methodModel.ReturnType);

                CollectFaultContracts(methodModel, callInfo);
                CollectStreamInfo(methodModel, callInfo);

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
            //var paramTypeFullName = type.ToString(); //type.ToDisplayString(FulluQualifiedSymbolFormat);

            return new ParamDeclaration(index, type.ToString(), paramName);
        }

        private void CollectFaultContracts(IMethodSymbol methodModel, OperationDeclaration callInfo)
        {
            foreach (var faultContractAttr in FindAttributes(methodModel, _faultContractAttribute).ToList())
            {
                var key = GetFaultKey(faultContractAttr.ConstructorArguments[0]);
                var faultType = GetFaultType(faultContractAttr.ConstructorArguments[1]);
                var faultTypeName = faultType.ToDisplayString(FulluQualifiedSymbolFormat);

                callInfo.AddFault(key, faultTypeName);
            }
        }

        private void CollectStreamInfo(IMethodSymbol methodModel, OperationDeclaration callInfo)
        {
            var inStreamAttr = FindAttribute(methodModel, _inputStreamAttribute);
            var outStreamAttr = FindAttribute(methodModel, _outputStreamAttribute);

            if (inStreamAttr != null)
            {
                var streamType = inStreamAttr.GetConstructorArgumentOrDefault<ITypeSymbol>(0);
                callInfo.InStreamItemType = streamType.ToDisplayString(FulluQualifiedSymbolFormat);
            }

            if (outStreamAttr != null)
            {
                var streamType = outStreamAttr.GetConstructorArgumentOrDefault<ITypeSymbol>(0);
                callInfo.OutStreamItemType = streamType.ToDisplayString(FulluQualifiedSymbolFormat);
            }
        }

        protected ushort GetCallKey(TypedConstant keyArg)
        {
            if (!(keyArg.Value is ushort))
                throw new Exception("Invalid property type!");

            return (ushort)keyArg.Value;
        }

        private ContractCallType GetCallType(TypedConstant typeArg)
        {
            if (typeArg.Kind != TypedConstantKind.Enum)
                throw new Exception("Invalid property type!");

            switch (typeArg.Value)
            {
                case 1: return ContractCallType.MessageToServer;
                case 2: return ContractCallType.CallToServer;
                case 3: return ContractCallType.CallToClient;
                case 4: return ContractCallType.MessageToClient;
            }

            throw new Exception("Unknonwn call type: " + typeArg.Value);
        }

        private ushort GetFaultKey(TypedConstant keyArg)
        {
            if (!(keyArg.Value is ushort))
                throw new Exception("Invalid property type!");

            return (ushort)keyArg.Value;
        }

        private ITypeSymbol GetFaultType(TypedConstant keyArg)
        {
            if (keyArg.Kind != TypedConstantKind.Type)
                throw new Exception("Invalid property type!");

            return (ITypeSymbol)keyArg.Value;
        }

        private void InitSharpRpcTypeSymbols(GeneratorExecutionContext context)
        {
            _contractAttrSymbol = GetSymbolOrThrow(Names.ServiceContractAttributeClass.Full, context);
            _rpcAttrSymbol = GetSymbolOrThrow(Names.RpcContractAttributeClass.Full, context);
            _serializerAttrSymbol = GetSymbolOrThrow(Names.RpcSerializerAttributeClass.Full, context);
            _faultContractAttribute = GetSymbolOrThrow(Names.RpcFaultAttributeClass.Full, context);
            _inputStreamAttribute = GetSymbolOrThrow(Names.RpcStreamInputAttributeClass.Full, context);
            _outputStreamAttribute = GetSymbolOrThrow(Names.RpcStreamOutputAttributeClass.Full, context);
        }

        private INamedTypeSymbol GetSymbolOrThrow(string metadataName, GeneratorExecutionContext context)
        {
            var symbol = context.Compilation.GetTypeByMetadataName(metadataName);
            if (symbol == null)
                throw new Exception("Cannot find type declaration: " + metadataName);
            return symbol;
        }

        private MethodDeclarationSyntax GenerateDescriptorFactoryMethod(ContractDeclaration contractInfo)
        {
            var msgFactoryCreationExp = SF.ObjectCreationExpression(
                SyntaxHelper.ShortTypeName(contractInfo.MessageFactoryClassName))
                .WithoutArguments();

            var retStatement = SF.ReturnStatement(
                SF.ObjectCreationExpression(SyntaxHelper.FullTypeName(Names.ContractDescriptorClass))
                .AddArgumentListArguments(SyntaxHelper.IdentifierArgument("sAdapter"), SF.Argument(msgFactoryCreationExp)));

            return SF.MethodDeclaration(SyntaxHelper.FullTypeName(Names.ContractDescriptorClass), Names.FacadeCreateDescriptorMethod)
                .AddModifiers(SF.Token(SyntaxKind.PrivateKeyword), SF.Token(SyntaxKind.StaticKeyword))
                .AddParameterListParameters(SyntaxHelper.Parameter("sAdapter", Names.RpcSerializerInterface.Full))
                .AddBodyStatements(retStatement);
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
