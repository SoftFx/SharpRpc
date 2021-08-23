﻿// Copyright © 2021 Soft-Fx. All rights reserved.
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

                var contracts = GetRpcContracts(context).ToList();

                foreach (var contractInfo in contracts)
                    GenerateStubs(contractInfo, context, diagnostics);

                diagnostics.DumpRecordsTo(context);
            }
            catch (Exception ex)
            {
                Trace.WriteLine("Exception in StubGenerator.Execute(): " + ex.Message);
                throw;
            }
        }

        private void GenerateStubs(ContractDeclaration contractInfo, GeneratorExecutionContext context, MetadataDiagnostics diagnostics)
        {
            var contractGenClassName = contractInfo.FacadeClassName;
            var hasPrebuilder = contractInfo.Calls.Any(c => c.EnablePrebuild && c.IsOneWay);
            var hasCallbacks = contractInfo.Calls.Any(c => c.IsCallback);

            var clientBuilder = new TxStubBuilder(contractInfo, false);
            var serverBuilder = new RxStubBuilder(contractInfo, false);

            var baseMsgNode = MessageBuilder.GenerateMessageBase(contractInfo);

            var systemMessageNodes = MessageBuilder
                .GenerateSystemMessages(contractInfo)
                .ToList();

            var userMessageNodes = MessageBuilder
                .GenerateUserMessages(contractInfo, context, diagnostics)
                .ToList();

            foreach (var msgNode in systemMessageNodes)
                baseMsgNode.Successors.Add(msgNode);

            foreach (var msgNode in userMessageNodes)
                baseMsgNode.Successors.Add(msgNode);

            var streamMessageNodes = (List<ClassBuildNode>)null;

            if (contractInfo.HasStreams)
            {
                streamMessageNodes = MessageBuilder
                    .GenerateStreamMessages(contractInfo)
                    .ToList();

                foreach (var msgNode in streamMessageNodes)
                    baseMsgNode.Successors.Add(msgNode);
            }
   
            var sFixture = new SerializerFixture()
                .AddHierachy(baseMsgNode);

            var sAdapterClasses = sFixture
                .GenerateSerializationAdapters(contractInfo, context)
                .ToArray();

            var baseMessageClass = baseMsgNode.CompleteBuilding();

            var systemMessageClasses = systemMessageNodes
                .Select(n => n.CompleteBuilding())
                .ToArray();

            var userMessageClasses = userMessageNodes
                .Select(n => n.CompleteBuilding())
                .ToArray();

            var systemBundleClass = SF.ClassDeclaration(contractInfo.SystemBundleClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddMembers(systemMessageClasses);

            var messageBundleClass = SF.ClassDeclaration(contractInfo.MessageBundleClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddMembers(baseMessageClass)
                .AddMembers(userMessageClasses);

            var streamBundleClass = (ClassDeclarationSyntax)null;

            if (contractInfo.HasStreams)
            {
                var streamMessageClasses = streamMessageNodes
                    .Select(n => n.CompleteBuilding())
                    .ToArray();

                var streamFactoryClasses = MessageBuilder
                    .GenerateStreamFactories(contractInfo)
                    .ToArray();

                streamBundleClass = SF.ClassDeclaration(contractInfo.StreamBundleClassName.Short)
                    .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                    .AddMembers(streamMessageClasses)
                    .AddMembers(streamFactoryClasses);
            }

            var messageFactoryClass = MessageBuilder.GenerateFactory(contractInfo);

            var clientFactoryMethod = clientBuilder.GenerateFactoryMethod();
            var sAdapterFactoryMethod = SerializerFixture.GenerateSerializerFactory(contractInfo);
            var descriptorFactoryMethod = GenerateDescriptorFactoryMethod(contractInfo);
            var serviceFactoryMethod = serverBuilder.GenerateBindMethod();

            var contractGenClass = SF.ClassDeclaration(contractGenClassName.Short)
               .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
               .AddMembers(clientFactoryMethod, serviceFactoryMethod, sAdapterFactoryMethod, descriptorFactoryMethod)
               .AddMembers(clientBuilder.GenerateCode(diagnostics), serverBuilder.GenerateServiceBase(), serverBuilder.GenerateHandler())
               .AddMembers(sAdapterClasses)
               .AddMembers(messageBundleClass, systemBundleClass, messageFactoryClass);

            if (hasCallbacks)
            {
                var callbackClientBuilder = new TxStubBuilder(contractInfo, true);
                var callbackServiceBuilder = new RxStubBuilder(contractInfo, true);
                var callbackClientClass = callbackClientBuilder.GenerateCode(diagnostics);
                var callbackServiceClass = callbackServiceBuilder.GenerateServiceBase();
                var handlerClass = callbackServiceBuilder.GenerateHandler();

                contractGenClass = contractGenClass.AddMembers(callbackClientClass, callbackServiceClass, handlerClass);
            }

            if (streamBundleClass != null)
                contractGenClass = contractGenClass.AddMembers(streamBundleClass);

            if (hasPrebuilder)
            {
                var prebuiltMessages = SerializerFixture.GeneratePrebuildMessages(contractInfo).ToArray();

                var prebuiltMessageBundleClass = SF.ClassDeclaration(contractInfo.PrebuiltBundleClassName.Short)
                    .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                    .AddMembers(prebuiltMessages);

                var prebuildClass = SerializerFixture.GeneratePrebuildTool(contractInfo);
                contractGenClass = contractGenClass.AddMembers(prebuildClass, prebuiltMessageBundleClass);
            }

            var stubNamespace = SF.NamespaceDeclaration(SF.IdentifierName(contractInfo.Namespace))
                                .AddMembers(contractGenClass);

            var compUnit = SF.CompilationUnit()
                .AddMembers(stubNamespace);

            var srcCode = compUnit
                .NormalizeWhitespace()
                .ToFullString();

            context.AddSource(contractGenClassName.Full, SourceText.From(srcCode, Encoding.UTF8));

            //#if DEBUG_BUILDER 
            //            var dbgFolder = Path.Combine("C:\\Temp\\SharpRpc Debug\\", context.Compilation.Assembly.Name);
            //            var pathToSave = Path.Combine(dbgFolder, contractGenClassName.Full + ".cs");
            //            Directory.CreateDirectory(dbgFolder);
            //            File.WriteAllText(pathToSave, srcCode);
            //#endif
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
                {
                    var contract = CollectContractData(interfaceDec, context, sm);
                    if (contract != null)
                        yield return contract;
                }
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

        private ContractDeclaration CollectContractData(InterfaceDeclarationSyntax contractTypeDec, GeneratorExecutionContext context, SemanticModel sm)
        {
            bool hasErrors = false;

            var contractSmbInfo = sm.GetDeclaredSymbol(contractTypeDec);
            var compatibilityAdapter = new ContractCompatibility(context);
            if (contractSmbInfo == null)
                return new ContractDeclaration("Error", compatibilityAdapter);
            var fullyQualifiedName = contractSmbInfo.ToDisplayString(FulluQualifiedSymbolFormat);
            var contractInfo = new ContractDeclaration(fullyQualifiedName, compatibilityAdapter);

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
                            contractInfo.Calls.Add(callData);
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

        private CallDeclaration CollectCallData(MethodDeclarationSyntax methodDec, SemanticModel sm, ContractDeclaration contract)
        {
            var methodModel = (IMethodSymbol)sm.GetDeclaredSymbol(methodDec);
            var callAttr = FindAttribute(methodModel, _rpcAttrSymbol);

            if (callAttr != null)
            {
                var typeArg = callAttr.ConstructorArguments[0];

                if (typeArg.Kind != TypedConstantKind.Enum)
                    throw new Exception("Invalid property type!");

                var callType = GetCallType(typeArg.Value);
                var callInfo = new CallDeclaration(methodModel.Name, methodModel.Locations[0], callType);

                int index = 1;
                foreach (var paramModel in methodModel.Parameters)
                    callInfo.Params.Add(CollectParamInfo(index++, paramModel));

                if (!methodModel.ReturnsVoid)
                    callInfo.ReturnParam = CollectParamInfo(0, methodModel.ReturnType);

                callInfo.EnablePrebuild = callAttr.GetNamedArgumentOrDefault<bool>(Names.PrebuildCallOption);

                CollectFaultContracts(methodModel, callInfo, contract);
                CollectStreamInfo(methodModel, callInfo, contract);

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

        private void CollectFaultContracts(IMethodSymbol methodModel, CallDeclaration callInfo, ContractDeclaration contract)
        {
            foreach (var faultContractAttr in FindAttributes(methodModel, _faultContractAttribute).ToList())
            {
                var faultDataTypes = faultContractAttr.GetConstructorArgumentArray<ITypeSymbol>(0);

                if (faultDataTypes != null)
                {
                    foreach (var faultType in faultDataTypes)
                    {
                        var faultTypeName = faultType.ToDisplayString(FulluQualifiedSymbolFormat);

                        if (!callInfo.Faults.Contains(faultTypeName))
                        {
                            callInfo.Faults.Add(faultTypeName);
                            contract.RegisterFault(faultTypeName);
                        }
                    }
                }
            }
        }

        private void CollectStreamInfo(IMethodSymbol methodModel, CallDeclaration callInfo, ContractDeclaration contract)
        {
            var inStreamAttr = FindAttribute(methodModel, _inputStreamAttribute);
            var outStreamAttr = FindAttribute(methodModel, _outputStreamAttribute);

            if (inStreamAttr != null)
            {
                var streamType = inStreamAttr.GetConstructorArgumentOrDefault<ITypeSymbol>(0);
                callInfo.InStreamItemType = streamType.ToDisplayString(FulluQualifiedSymbolFormat);
                contract.RegisterStreamType(callInfo.InStreamItemType);
            }

            if (outStreamAttr != null)
            {
                var streamType = outStreamAttr.GetConstructorArgumentOrDefault<ITypeSymbol>(0);
                callInfo.OutStreamItemType = streamType.ToDisplayString(FulluQualifiedSymbolFormat);
                contract.RegisterStreamType(callInfo.OutStreamItemType);
            }
        }

        //private bool TryGetAttributeValue(AttributeData data, string name, out TypedConstant value)
        //{
        //    foreach (var arg in data.NamedArguments)
        //    {
        //        if (arg.Key == name)
        //        {
        //            value = arg.Value;
        //            return true;
        //        }
        //    }

        //    value = default(TypedConstant);
        //    return false;
        //}

        private ContractCallType GetCallType(object value)
        {
            switch (value)
            {
                case 1: return ContractCallType.MessageToServer;
                case 2: return ContractCallType.CallToServer;
                case 3: return ContractCallType.CallToClient;
                case 4: return ContractCallType.MessageToClient;
            }

            throw new Exception("Unknonwn call type: " + value);
        }

        private void InitSharpRpcTypeSymbols(GeneratorExecutionContext context)
        {
            _contractAttrSymbol = GetSymbolOrThrow(Names.ContractAttributeClass.Full, context);
            _rpcAttrSymbol = GetSymbolOrThrow(Names.RpcAttributeClass.Full, context);
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
