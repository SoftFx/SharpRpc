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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using SH = SharpRpc.Builder.SyntaxHelper;

namespace SharpRpc.Builder
{
    public enum MessageType
    {
        OneWay,
        Request,
        Response,
        Fault,
        System
    }

    internal class MessageBuilder
    {
        public const int LoginMessageKey = 1;
        public const int LogoutMessageKey = 2;
        public const int HeartbeatMessageKey = 3;
        public const int CancelRequestMessageKey = 4;
        public const int CancelStreamMessageKey = 5;
        public const int ConfirmResponseMessageKey = 6;
        public const int StreamAckMessageKey = 7;
        public const int StreamCloseMessageKey = 8;
        public const int StreamCancelMessageKey = 9;
        public const int StreamCloseAckMessageKey = 10;

        internal MessageBuilder(ContractDeclaration contract, OperationDeclaration callDec, MessageType type)
        {
            ContractInfo = contract;
            RpcInfo = callDec;
            MessageType = type;
        }

        public OperationDeclaration RpcInfo { get; }
        public ContractDeclaration ContractInfo { get; }
        public MessageType MessageType { get; }

        public static ClassBuildNode GenerateMessageBundle(ContractDeclaration contractInfo,
            SerializerFixture sRegistry, MetadataDiagnostics diagnostics)
        {
            var factoryInterface = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.MessageFactoryInterface));

            var baseMsgNode = GenerateMessageBase(contractInfo);

            var messageBundleClass = SyntaxFactory.ClassDeclaration(contractInfo.MessageBundleClassName.Short)
                .AddBaseListTypes(factoryInterface)
                .AddModifiers(SyntaxHelper.PublicToken());

            var messageBundleNode = new ClassBuildNode(0, contractInfo.MessageBundleClassName, messageBundleClass)
                .AddMethods(GenerateFactoryMethods(contractInfo))
                .AddNestedClass(baseMsgNode)
                .AddNestedClasses(GenerateStreamFactories(contractInfo));

            sRegistry.RegisterSerializableClass(baseMsgNode);

            foreach (var message in GenerateMessages(contractInfo, sRegistry, diagnostics))
            {
                messageBundleNode.AddNestedClass(message);
                sRegistry.RegisterSerializableClass(message);
                message.RegisterBaseClass(baseMsgNode);
            }

            return messageBundleNode;
        }

        private static IEnumerable<ClassBuildNode> GenerateMessages(ContractDeclaration contract, SerializerFixture sRegistry, MetadataDiagnostics diagnostics)
        {
            foreach (var message in GenerateSystemMessages(contract))
                yield return message;

            foreach (var message in GenerateUserMessages(contract, sRegistry, diagnostics))
                yield return message;
        }

        public static IEnumerable<ClassDeclarationSyntax> GeneratePrebuildMessages(ContractDeclaration contract)
        {
            bool singleAdapter = contract.Serializers.Count == 1;

            foreach (var callDef in contract.Operations)
            {
                if (callDef.IsOneWay && contract.EnablePrebuild)
                {
                    var msgName = contract.GetPrebuiltMessageClassName(callDef.MethodName);
                    var baseType = singleAdapter ? Names.RpcPrebuiltMessage : Names.RpcMultiPrebuiltMessage;

                    var constructorInitializer = SF.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
                        .AddArgumentListArguments(SH.IdentifierArgument("bytes"));

                    var bytesParam = SH.Parameter("bytes", Names.RpcSegmentedByteArray.Full);

                    var constructor = SF.ConstructorDeclaration(msgName.Short)
                        .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                        .AddParameterListParameters(bytesParam)
                        .WithInitializer(constructorInitializer)
                        .WithBody(SF.Block());

                    var nameProperty = CreateContractMessageNameProperty(callDef, "P")
                        .AddModifiers(SH.OverrideToken());

                    yield return SF.ClassDeclaration(msgName.Short)
                        .AddBaseListTypes(SF.SimpleBaseType(SH.FullTypeName(baseType)))
                        .AddModifiers(SH.PublicToken())
                        .AddMembers(nameProperty)
                        .AddMembers(constructor);
                }
            }
        }

        private static IEnumerable<ClassBuildNode> GenerateStreamFactories(ContractDeclaration contract)
        {
            foreach (var opContract in contract.Operations)
            {
                if (opContract.HasInStream)
                {
                    yield return GenerateStreamFactory(contract, contract.GetInputStreamFactoryClassName(opContract),
                        contract.GetInputStreamMessageClassName(opContract), opContract.InStreamItemType);
                }

                if (opContract.HasOutStream)
                {
                    yield return GenerateStreamFactory(contract, contract.GetOutputStreamFactoryClassName(opContract),
                        contract.GetOutputStreamMessageClassName(opContract), opContract.OutStreamItemType);
                }
            }
        }

        private static IEnumerable<ClassBuildNode> GenerateSystemMessages(ContractDeclaration contract)
        {
            yield return GenerateLoginMessage(contract);
            yield return GenerateLogoutMessage(contract);
            yield return GenerateHeartbeatMessage(contract);
            yield return GenerateStreamPageAckMessage(contract);
            yield return GenerateStreamCancelMessage(contract);
            yield return GenerateStreamCloseMessage(contract);
            yield return GenerateStreamCloseAckMessage(contract);
            yield return GenerateCancelRequestMessage(contract);
        }

        private static IEnumerable<ClassBuildNode> GenerateUserMessages(ContractDeclaration contract, SerializerFixture sRegistry, MetadataDiagnostics diagnostics)
        {
            foreach (var opContract in contract.Operations)
            {
                if (opContract.IsRequestResponceCall)
                {
                    yield return new MessageBuilder(contract, opContract, MessageType.Request).GenerateMessage(sRegistry);
                    yield return new MessageBuilder(contract, opContract, MessageType.Response).GenerateMessage(sRegistry);
                    yield return new MessageBuilder(contract, opContract, MessageType.Fault).GenerateMessage(sRegistry);

                    if (opContract.HasInStream)
                    {
                        yield return GenerateStreamPageMessage(opContract.InStreamPageKey,
                            contract.GetInputStreamMessageClassName(opContract), contract, opContract.InStreamItemType, opContract);
                    }

                    if (opContract.HasOutStream)
                    {
                        yield return GenerateStreamPageMessage(opContract.OutStreamPageKey,
                             contract.GetOutputStreamMessageClassName(opContract), contract, opContract.OutStreamItemType, opContract);
                    }
                }
                else
                {
                    if (opContract.ReturnsData)
                        diagnostics.AddOneWayReturnsDataWarning(opContract.CodeLocation, opContract.MethodName);

                    yield return new MessageBuilder(contract, opContract, MessageType.OneWay).GenerateMessage(sRegistry);
                }
            }
        }

        public static IEnumerable<MethodDeclarationSyntax> GenerateFactoryMethods(ContractDeclaration contractInfo)
        {
            yield return GenerateFactoryMethod("CreateLoginMessage", Names.LoginMessageInterface, contractInfo.LoginMessageClassName);
            yield return GenerateFactoryMethod("CreateLogoutMessage", Names.LogoutMessageInterface, contractInfo.LogoutMessageClassName);
            yield return GenerateFactoryMethod("CreateHeartBeatMessage", Names.HeartbeatMessageInterface, contractInfo.HeartbeatMessageClassName);
            yield return GenerateFactoryMethod("CreateCancelRequestMessage", Names.CancelRequestMessageInterface, contractInfo.CancelRequestMessageClassName);
        }

        private static MethodDeclarationSyntax GenerateFactoryMethod(string methodName, TypeString retType, TypeString messageType)
        {
            var retStatement = SyntaxFactory.ReturnStatement(
                SyntaxFactory.ObjectCreationExpression(SyntaxHelper.FullTypeName(messageType))
                    .WithoutArguments());

            return SyntaxFactory.MethodDeclaration(SyntaxHelper.FullTypeName(retType), methodName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBodyStatements(retStatement);
        }

        private static ClassBuildNode GenerateMessageBase(ContractDeclaration contract)
        {
            var baseMessageClassName = contract.BaseMessageClassName;

            var msgDeclaration = SyntaxFactory.InterfaceDeclaration(baseMessageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxHelper.GlobalTypeName(Names.MessageInterface)));

            return new ClassBuildNode(0, baseMessageClassName, msgDeclaration);
        }

        private static ClassBuildNode GenerateLoginMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.LoginMessageClassName;

            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(contractInfo.BaseMessageClassName));
            var iLoginBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.LoginMessageInterface));

            var userNameProperty = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName("string"), "UserName")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var passwordProperty = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName("string"), "Password")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var resultPropertyType = SyntaxFactory.NullableType(SyntaxHelper.FullTypeName(Names.LoginResultEnum));
            var resultProperty = SyntaxFactory
                .PropertyDeclaration(resultPropertyType, "ResultCode")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var errorMessageProperty = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName("string"), "ErrorMessage")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iLoginBase);

            return new ClassBuildNode(LoginMessageKey, messageClassName, messageClassDeclaration)
                .AddAuxProperties(CreateSystemMessageNameProperty(contractInfo.LoginMessageClassName.Short))
                .AddProperties(userNameProperty, passwordProperty, resultProperty, errorMessageProperty);
        }

        private static ClassBuildNode GenerateLogoutMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.LogoutMessageClassName;

            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(contractInfo.BaseMessageClassName));
            var iLogoutBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.LogoutMessageInterface));

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iLogoutBase);

            return new ClassBuildNode(LogoutMessageKey, messageClassName, messageClassDeclaration)
                .AddAuxProperties(CreateSystemMessageNameProperty(contractInfo.LogoutMessageClassName.Short));
        }

        private static ClassBuildNode GenerateHeartbeatMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.HeartbeatMessageClassName;

            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(contractInfo.BaseMessageClassName));
            var iHeartbeatBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.HeartbeatMessageInterface));

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iHeartbeatBase);

            return new ClassBuildNode(HeartbeatMessageKey, messageClassName, messageClassDeclaration)
                .AddAuxProperties(CreateSystemMessageNameProperty(contractInfo.HeartbeatMessageClassName.Short));
        }

        private static ClassBuildNode GenerateCancelRequestMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.CancelRequestMessageClassName;

            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(contractInfo.BaseMessageClassName));
            var iCancelRequestBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.CancelRequestMessageInterface));

            var callIdProperty = GenerateMessageProperty("string", "CallId");

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iCancelRequestBase);

            return new ClassBuildNode(CancelRequestMessageKey, messageClassName, messageClassDeclaration)
                .AddAuxProperties(CreateSystemMessageNameProperty(contractInfo.CancelRequestMessageClassName.Short))
                .AddProperties(callIdProperty);
        }

        internal ClassBuildNode GenerateMessage(SerializerFixture serializerRegistry)
        {
            var messageClassName = GetMessageClassName(out var messageKey);

            var baseTypes = new List<BaseTypeSyntax>();
            var nestedClasses = new List<ClassBuildNode>();

            baseTypes.Add(SyntaxFactory.SimpleBaseType(SyntaxHelper.ShortTypeName(ContractInfo.BaseMessageClassName)));

            if (MessageType == MessageType.Request)
            {
                if (RpcInfo.HasStreams)
                    baseTypes.Add(SyntaxFactory.SimpleBaseType(SyntaxHelper.GlobalTypeName(Names.StreamRequestInterface)));
                else
                    baseTypes.Add(SyntaxFactory.SimpleBaseType(SyntaxHelper.GlobalTypeName(Names.RequestInterface)));
            }
            else if (MessageType == MessageType.Response)
            {
                if (RpcInfo.ReturnsData)
                    baseTypes.Add(SyntaxFactory.SimpleBaseType(SyntaxHelper.GenericType(Names.ResponseInterface.Full, RpcInfo.ReturnParam.ParamType)));
                else
                    baseTypes.Add(SyntaxFactory.SimpleBaseType(SyntaxHelper.GlobalTypeName(Names.ResponseInterface)));
            }
            else if (MessageType == MessageType.Fault)
                baseTypes.Add(SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.FaultMessageInterface)));

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(baseTypes.ToArray());

            var properties = new List<PropertyDeclarationSyntax>();

            if (MessageType == MessageType.Request || MessageType == MessageType.Response || MessageType == MessageType.Fault)
            {
                properties.Add(GenerateMessageProperty("string", "CallId"));
            }

            if (MessageType == MessageType.Request)
            {
                properties.Add(GenerateMessageProperty("SharpRpc.RequestOptions", "Options"));

                if (RpcInfo.HasStreams)
                    properties.Add(GenerateMessageProperty("ushort?", "WindowSize"));
            }

            if (MessageType == MessageType.Request || MessageType == MessageType.OneWay)
            {
                var index = 1;

                foreach (var param in RpcInfo.Params)
                    properties.Add(GenerateMessageProperty(param, index++));
            }
            else if (MessageType == MessageType.Response)
            {
                if (RpcInfo.ReturnsData)
                    properties.Add(GenerateMessageProperty(RpcInfo.ReturnParam.ParamType, Names.ResponseResultProperty));
            }
            else if (MessageType == MessageType.Fault)
            {
                properties.Add(GenerateMessageProperty("string", "Text"));
                properties.Add(GenerateMessageProperty("SharpRpc.RequestFaultCode", "Code"));

                if (RpcInfo.CustomFaults.Count > 0)
                {
                    nestedClasses.AddRange(GenerateCustomFaultFixtureClasses(serializerRegistry));
                    properties.Add(GenerateMessageProperty(RpcInfo.FaultAdapterInterfaceName, "CustomFaultBinding"));
                }

                messageClassDeclaration = messageClassDeclaration.AddMembers(GenerateGetCustomFaultMethod());
            }

            return new ClassBuildNode(messageKey, messageClassName, messageClassDeclaration)
                .AddAuxProperties(CreateContractMessageNameProperty())
                .AddProperties(properties)
                .AddNestedClasses(nestedClasses);
        }

        private TypeString GetMessageClassName(out int messageKey)
        {
            if (MessageType == MessageType.OneWay)
            {
                messageKey = RpcInfo.RequestKey;
                return ContractInfo.GetOnWayMessageClassName(RpcInfo);
            }
            else if (MessageType == MessageType.Request)
            {
                messageKey = RpcInfo.RequestKey;
                return ContractInfo.GetRequestClassName(RpcInfo);
            }
            else if (MessageType == MessageType.Response)
            {
                messageKey = RpcInfo.ResponseKey;
                return ContractInfo.GetResponseClassName(RpcInfo);
            }
            else
            {
                messageKey = RpcInfo.FaultKey;
                return ContractInfo.GetFaultMessageClassName(RpcInfo);
            }
        }

        private static PropertyDeclarationSyntax GenerateMessageProperty(string type, string name)
        {
            return SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(type), name)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();
        }

        private PropertyDeclarationSyntax GenerateMessageProperty(ParamDeclaration callProp, int index)
        {
            return SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(callProp.ParamType), callProp.MessagePropertyName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();
        }

        private MethodDeclarationSyntax GenerateGetCustomFaultMethod()
        {
            var hasCustomFaults = RpcInfo.CustomFaults.Count > 0;
            var faultDataRef = hasCustomFaults
                ? SyntaxFactory.IdentifierName("CustomFaultBinding")
                : (ExpressionSyntax)SyntaxHelper.NullLiteral();

            var retBindingPropStatement = SyntaxFactory.ReturnStatement(faultDataRef);

            var retType = SyntaxHelper.FullTypeName(Names.CustomFaultBindingInterface);

            return SyntaxFactory.MethodDeclaration(retType, "GetCustomFaultBinding")
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddBodyStatements(retBindingPropStatement);
        }

        private IEnumerable<ClassBuildNode> GenerateCustomFaultFixtureClasses(SerializerFixture serializerRegistry)
        {
            var classes = new List<ClassBuildNode>();
            var adapterInterface = GenerateCustomFaultAdapterInterface();

            classes.Add(adapterInterface);
            serializerRegistry.RegisterSerializableClass(adapterInterface);

            yield return adapterInterface;

            foreach (var adapter in GenerateCustomFaultAdapters())
            {
                serializerRegistry.RegisterSerializableClass(adapter);
                adapter.RegisterBaseClass(adapterInterface);

                yield return adapter;
            }
        }

        private ClassBuildNode GenerateCustomFaultAdapterInterface()
        {
            var adapterInterfaceName = new TypeString("", RpcInfo.FaultAdapterInterfaceName);

            var adapterInterfaceDeclaration = SyntaxFactory.InterfaceDeclaration(adapterInterfaceName.Short)
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.CustomFaultBindingInterface)))
                .AddMembers();

            return new ClassBuildNode(0, adapterInterfaceName, adapterInterfaceDeclaration);
        }

        private IEnumerable<ClassBuildNode> GenerateCustomFaultAdapters()
        {
            foreach (var customFaultDeclaration in RpcInfo.CustomFaults)
            {
                var key = customFaultDeclaration.Item1;
                var dataType = customFaultDeclaration.Item2;

                var adapterClassName = ContractInfo.GetFaultAdapterClassName(key, RpcInfo);
                var interfaceName = RpcInfo.FaultAdapterInterfaceName;

                var getFaultReturnStatement = SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("Data"));
                var getFaultMethod = SyntaxFactory.MethodDeclaration(SyntaxFactory.IdentifierName("object"), "GetFault")
                    .AddModifiers(SyntaxHelper.PublicToken())
                    .AddBodyStatements(getFaultReturnStatement);

                var exceptionCreationStatemnt = SyntaxFactory.ReturnStatement(
                    SyntaxFactory.ObjectCreationExpression(SyntaxHelper.GenericType(Names.RpcFaultException.Full, dataType))
                    .AddArgumentListArguments(SyntaxHelper.IdentifierArgument("text"), SyntaxHelper.IdentifierArgument("Data")));

                var createExceprtionMethod = SyntaxFactory.MethodDeclaration(SyntaxHelper.FullTypeName(Names.RpcFaultException), "CreateException")
                    .AddModifiers(SyntaxHelper.PublicToken())
                    .AddParameterListParameters(SyntaxHelper.Parameter("text", "string"))
                    .AddBodyStatements(exceptionCreationStatemnt);

                var adapterClass = SyntaxFactory.ClassDeclaration(adapterClassName.Short)
                    .AddModifiers(SyntaxHelper.PublicToken())
                    .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.IdentifierName(interfaceName)));

                var dataProp = GenerateMessageProperty(dataType, "Data");

                yield return new ClassBuildNode(key, adapterClassName, adapterClass)
                    .AddProperties(dataProp)
                    .AddMethods(getFaultMethod, createExceprtionMethod);
            }
        }

        private static ClassBuildNode GenerateStreamPageMessage(int messageKey, TypeString messageClassName, ContractDeclaration contractInfo, string streamType, OperationDeclaration opInfo)
        {
            //var messageClassName = contractInfo.GetStreamPageClassName(streamType);
            var genericListType = SyntaxHelper.GenericType("System.Collections.Generic.List", streamType);

            var streamIdParam = SyntaxHelper.Parameter("streamId", "string");
            var streamIdInitStatement = SyntaxHelper.AssignmentStatement(SyntaxFactory.IdentifierName("CallId"), SyntaxFactory.IdentifierName("streamId"));
            var constructor = SyntaxFactory.ConstructorDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(streamIdInitStatement);

            var streamIdProperty = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName("string"), "CallId")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var itemsProperty = SyntaxFactory
                .PropertyDeclaration(genericListType, "Items")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(contractInfo.BaseMessageClassName));
            var iStreamBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.GenericName(Names.StreamPageInterface.Full, streamType));

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iStreamBase)
                .AddMembers(constructor);

            return new ClassBuildNode(messageKey, messageClassName, messageClassDeclaration)
                .AddAuxProperties(CreateContractMessageNameProperty(opInfo, "DataPage"))
                .AddProperties(streamIdProperty, itemsProperty);
        }

        private static ClassBuildNode GenerateStreamPageAckMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.StreamPageAckMessageClassName;

            var streamIdParam = SyntaxHelper.Parameter("streamId", "string");
            var streamIdInitStatement = SyntaxHelper.AssignmentStatement(SyntaxFactory.IdentifierName("CallId"), SyntaxFactory.IdentifierName("streamId"));
            var constructor = SyntaxFactory.ConstructorDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(streamIdInitStatement);

            var streamIdProperty = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName("string"), "CallId")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var pagesConsumedProperty = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName("ushort"), "Consumed")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(contractInfo.BaseMessageClassName));
            var iStreamAckBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.StreamPageAckInterface));

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iStreamAckBase)
                .AddMembers(constructor);

            return new ClassBuildNode(StreamAckMessageKey, messageClassName, messageClassDeclaration)
                .AddAuxProperties(CreateSystemMessageNameProperty("PageAcknowledgement"))
                .AddProperties(streamIdProperty, pagesConsumedProperty);
        }

        private static ClassBuildNode GenerateStreamCancelMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.StreamCancelMessageClassName;

            var streamIdParam = SyntaxHelper.Parameter("streamId", "string");
            var streamIdInitStatement = SyntaxHelper.AssignmentStatement(SyntaxFactory.IdentifierName("CallId"), SyntaxFactory.IdentifierName("streamId"));
            var constructor = SyntaxFactory.ConstructorDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(streamIdInitStatement);

            var streamIdProperty = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName("string"), "CallId")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var optionsProperty = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName("SharpRpc.StreamCancelOptions"), "Options")
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddAutoGetter()
                .AddAutoSetter();

            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(contractInfo.BaseMessageClassName));
            var iStreamComplBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.StreamCancelMessageInterface));

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iStreamComplBase)
                .AddMembers(constructor);

            return new ClassBuildNode(StreamCancelMessageKey, messageClassName, messageClassDeclaration)
                .AddAuxProperties(CreateContractMessageNameProperty("CancelStream"))
                .AddProperties(streamIdProperty, optionsProperty);
        }

        private static ClassBuildNode GenerateStreamCloseMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.StreamCloseMessageClassName;

            var streamIdParam = SyntaxHelper.Parameter("streamId", "string");
            var streamIdInitStatement = SyntaxHelper.AssignmentStatement(SyntaxFactory.IdentifierName("CallId"), SyntaxFactory.IdentifierName("streamId"));
            var constructor = SyntaxFactory.ConstructorDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(streamIdInitStatement);

            var streamIdProperty = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName("string"), "CallId")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var optionsProperty = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName("SharpRpc.StreamCloseOptions"), "Options")
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddAutoGetter()
                .AddAutoSetter();

            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(contractInfo.BaseMessageClassName));
            var iStreamComplBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.StreamCloseMessageInterface));

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iStreamComplBase)
                .AddMembers(constructor);

            return new ClassBuildNode(StreamCloseMessageKey, messageClassName, messageClassDeclaration)
                .AddAuxProperties(CreateContractMessageNameProperty("CloseStream"))
                .AddProperties(streamIdProperty, optionsProperty);
        }

        private static ClassBuildNode GenerateStreamCloseAckMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.StreamCloseAckMessageClassName;

            var streamIdParam = SyntaxHelper.Parameter("streamId", "string");
            var streamIdInitStatement = SyntaxHelper.AssignmentStatement(SyntaxFactory.IdentifierName("CallId"), SyntaxFactory.IdentifierName("streamId"));
            var constructor = SyntaxFactory.ConstructorDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(streamIdInitStatement);

            var streamIdProperty = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName("string"), "CallId")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(contractInfo.BaseMessageClassName));
            var iStreamComplBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.StreamCloseAckMessageInterface));

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iStreamComplBase)
                .AddMembers(constructor);

            return new ClassBuildNode(StreamCloseAckMessageKey, messageClassName, messageClassDeclaration)
                .AddAuxProperties(CreateContractMessageNameProperty("CloseStreamAck"))
                .AddProperties(streamIdProperty);
        }

        private static ClassBuildNode GenerateStreamFactory(ContractDeclaration contractInfo,
            TypeString factoryClassName, TypeString pageClassName, string streamType)
        {
            //var factoryClassName = contractInfo.GetStreamFactoryClassName(streamType);
            var factoryInterface = SyntaxHelper.GenericName(Names.StreamFactoryInterface.Full, streamType);

            var streamIdParam = SyntaxHelper.Parameter("streamId", "string");

            // page factory method

            var pageCreationStatement = SyntaxFactory.ReturnStatement(
                SyntaxFactory.ObjectCreationExpression(SyntaxHelper.ShortTypeName(pageClassName))
                    .AddArgumentListArguments(SyntaxHelper.IdentifierArgument("streamId")));

            var createPageRetType = SyntaxHelper.GenericType(Names.StreamPageInterface.Full, streamType);
            var createPageMethod = SyntaxFactory.MethodDeclaration(createPageRetType, "CreatePage")
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(pageCreationStatement);

            // acknowledgement factory method

            var ackClassName = contractInfo.StreamPageAckMessageClassName;
            var ackCreationStatement = SyntaxFactory.ReturnStatement(
                SyntaxFactory.ObjectCreationExpression(SyntaxHelper.ShortTypeName(ackClassName))
                    .AddArgumentListArguments(SyntaxHelper.IdentifierArgument("streamId")));

            var createAckRetType = SyntaxFactory.IdentifierName(Names.StreamPageAckInterface.Full);
            var createAckMethod = SyntaxFactory.MethodDeclaration(createAckRetType, "CreatePageAcknowledgement")
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(ackCreationStatement);

            // close message factory method

            var closeMessageType = SyntaxHelper.ShortTypeName(contractInfo.StreamCloseMessageClassName);
            var closeMessageCreationStatement = SyntaxFactory.ReturnStatement(
                SyntaxFactory.ObjectCreationExpression(closeMessageType)
                    .AddArgumentListArguments(SyntaxHelper.IdentifierArgument("streamId")));

            var createCloseMsgRetType = SyntaxFactory.IdentifierName(Names.StreamCloseMessageInterface.Full);
            var createCloseMsgMethod = SyntaxFactory.MethodDeclaration(createCloseMsgRetType, "CreateCloseMessage")
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(closeMessageCreationStatement);

            // close ack message factory method

            var closeAckMessageType = SyntaxHelper.ShortTypeName(contractInfo.StreamCloseAckMessageClassName);
            var closeAckMessageCreationStatement = SyntaxFactory.ReturnStatement(
                SyntaxFactory.ObjectCreationExpression(closeAckMessageType)
                    .AddArgumentListArguments(SyntaxHelper.IdentifierArgument("streamId")));

            var createCloseAckMsgRetType = SyntaxFactory.IdentifierName(Names.StreamCloseAckMessageInterface.Full);
            var createCloseAckMsgMethod = SyntaxFactory.MethodDeclaration(createCloseAckMsgRetType, "CreateCloseAcknowledgement")
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(closeAckMessageCreationStatement);

            // cancel message factory method

            var cancelRequestMessageType = SyntaxHelper.ShortTypeName(contractInfo.StreamCancelMessageClassName);
            var cancelMessageCreationStatement = SyntaxFactory.ReturnStatement(
                SyntaxFactory.ObjectCreationExpression(cancelRequestMessageType)
                    .AddArgumentListArguments(SyntaxHelper.IdentifierArgument("streamId")));

            var createCancelMsgRetType = SyntaxFactory.IdentifierName(Names.StreamCancelMessageInterface.Full);
            var createCancelMsgMethod = SyntaxFactory.MethodDeclaration(createCancelMsgRetType, "CreateCancelMessage")
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(cancelMessageCreationStatement);

            // class declaration

            var classDec = SyntaxFactory.ClassDeclaration(factoryClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SyntaxFactory.SimpleBaseType(factoryInterface))
                .AddMembers(createPageMethod, createCloseMsgMethod, createCloseAckMsgMethod, createCancelMsgMethod, createAckMethod);

            return new ClassBuildNode(0, factoryClassName, classDec);
        }

        private PropertyDeclarationSyntax CreateContractMessageNameProperty()
        {
            return CreateContractMessageNameProperty(RpcInfo, MessageType.ToString());
        }

        private static PropertyDeclarationSyntax CreateContractMessageNameProperty(OperationDeclaration opInfo, string msgSubType)
        {
            return CreateContractMessageNameProperty($"{opInfo.MethodName}#{msgSubType}");
        }

        private static PropertyDeclarationSyntax CreateSystemMessageNameProperty(string msgTypeName)
        {
            return CreateContractMessageNameProperty($"#{msgTypeName}");
        }

        private static PropertyDeclarationSyntax CreateContractMessageNameProperty(string name)
        {
            return SyntaxHelper.CreateLambdaGetOnlyProperty("string", "ContractMessageName", SyntaxHelper.LiteralExpression(name))
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword));
        }
    }
}
