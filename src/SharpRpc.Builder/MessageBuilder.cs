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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

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
        public const int StreamAckMessageKey = 4;
        public const int StreamCompletionMessageKey = 5;

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
            var baseMsgNode = GenerateMessageBase(contractInfo);

            var messageBundleClass = SyntaxFactory.ClassDeclaration(contractInfo.MessageBundleClassName.Short)
                .AddModifiers(SyntaxHelper.PublicToken());

            var messageBundleNode = new ClassBuildNode(0, contractInfo.MessageBundleClassName, messageBundleClass)
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
            yield return GenerateStreamAcknowledgementMessage(contract);
            yield return GenerateStreamCompletionMessage(contract);
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
                            contract.GetInputStreamMessageClassName(opContract), contract, opContract.InStreamItemType);
                    }

                    if (opContract.HasOutStream)
                    {
                        yield return GenerateStreamPageMessage(opContract.OutStreamPageKey,
                             contract.GetOutputStreamMessageClassName(opContract), contract, opContract.OutStreamItemType);
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

        public static ClassDeclarationSyntax GenerateFactory(ContractDeclaration contractInfo)
        {
            var factoryInterface = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.MessageFactoryInterface));

            var loginMsgMethod = GenerateFactoryMethod("CreateLoginMessage", Names.LoginMessageInterface, contractInfo.LoginMessageClassName);
            var logoutMsgMethod = GenerateFactoryMethod("CreateLogoutMessage", Names.LogoutMessageInterface, contractInfo.LogoutMessageClassName);
            var heartbeatMsgMethod = GenerateFactoryMethod("CreateHeartBeatMessage", Names.HeartbeatMessageInterface, contractInfo.HeartbeatMessageClassName);
            //var faultFactoryMethod = GenerateFaultFactory(contractInfo);
            //var customFaultsMethod = GenerateCustomFaultFactory(contractInfo);
            //var basicAuthMethod = GenerateFactoryMethod("CreateBasicAuthData", Names.BasicAuthDataInterface, contractInfo.BasicAuthDataClassName);

            return SyntaxFactory.ClassDeclaration(contractInfo.MessageFactoryClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
                .AddBaseListTypes(factoryInterface)
                .AddMembers(loginMsgMethod, logoutMsgMethod, heartbeatMsgMethod);
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

            return new ClassBuildNode(LogoutMessageKey, messageClassName, messageClassDeclaration);
        }

        private static ClassBuildNode GenerateHeartbeatMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.HeartbeatMessageClassName;

            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(contractInfo.BaseMessageClassName));
            var iHeartbeatBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.HeartbeatMessageInterface));

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iHeartbeatBase);

            return new ClassBuildNode(HeartbeatMessageKey, messageClassName, messageClassDeclaration);
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

        private PropertyDeclarationSyntax GenerateMessageProperty(string type, string name)
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

        private static ClassBuildNode GenerateStreamPageMessage(int messageKey, TypeString messageClassName, ContractDeclaration contractInfo, string streamType)
        {
            //var messageClassName = contractInfo.GetStreamPageClassName(streamType);
            var genericListType = SyntaxHelper.GenericType("System.Collections.Generic.List", streamType);

            var streamIdParam = SyntaxHelper.Parameter("streamId", "string");
            var streamIdInitStatement = SyntaxHelper.AssignmentStatement(SyntaxFactory.IdentifierName("StreamId"), SyntaxFactory.IdentifierName("streamId"));
            var constructor = SyntaxFactory.ConstructorDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(streamIdInitStatement);

            var streamIdProperty = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName("string"), "StreamId")
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
                .AddProperties(streamIdProperty, itemsProperty);
        }

        private static ClassBuildNode GenerateStreamAcknowledgementMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.StreamPageAckMessageClassName;

            var streamIdParam = SyntaxHelper.Parameter("streamId", "string");
            var streamIdInitStatement = SyntaxHelper.AssignmentStatement(SyntaxFactory.IdentifierName("StreamId"), SyntaxFactory.IdentifierName("streamId"));
            var constructor = SyntaxFactory.ConstructorDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(streamIdInitStatement);

            var streamIdProperty = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName("string"), "StreamId")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var pagesConsumedProperty = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName("ushort"), "PagesConsumed")
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
                .AddProperties(streamIdProperty, pagesConsumedProperty);
        }

        private static ClassBuildNode GenerateStreamCompletionMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.StreamCompletionMessageClassName;

            var streamIdParam = SyntaxHelper.Parameter("streamId", "string");
            var streamIdInitStatement = SyntaxHelper.AssignmentStatement(SyntaxFactory.IdentifierName("StreamId"), SyntaxFactory.IdentifierName("streamId"));
            var constructor = SyntaxFactory.ConstructorDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(streamIdInitStatement);

            var streamIdProperty = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName("string"), "StreamId")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(contractInfo.BaseMessageClassName));
            var iStreamComplBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.StreamCompletionMessageInterface));

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iStreamComplBase)
                .AddMembers(constructor);

            return new ClassBuildNode(StreamCompletionMessageKey, messageClassName, messageClassDeclaration)
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

            // completion factory method

            var completionMessageType = SyntaxHelper.ShortTypeName(contractInfo.StreamCompletionMessageClassName);
            var completionCreationStatement = SyntaxFactory.ReturnStatement(
                SyntaxFactory.ObjectCreationExpression(completionMessageType)
                    .AddArgumentListArguments(SyntaxHelper.IdentifierArgument("streamId")));

            var createCompletionRetType = SyntaxFactory.IdentifierName(Names.StreamCompletionMessageInterface.Full);
            var createCompletionMethod = SyntaxFactory.MethodDeclaration(createCompletionRetType, "CreateCompletionMessage")
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(completionCreationStatement);

            // class declaration

            var classDec = SyntaxFactory.ClassDeclaration(factoryClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SyntaxFactory.SimpleBaseType(factoryInterface))
                .AddMembers(createPageMethod, createCompletionMethod, createAckMethod);

            return new ClassBuildNode(0, factoryClassName, classDec);
        }
    }
}
