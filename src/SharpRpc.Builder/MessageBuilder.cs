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
        public const int LogoutRequestMessageKey = 11;

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
            var factoryInterface = SF.SimpleBaseType(SH.FullTypeName(Names.MessageFactoryInterface));

            var baseMsgNode = GenerateMessageBase(contractInfo);

            var messageBundleClass = SF.ClassDeclaration(contractInfo.MessageBundleClassName.Short)
                .AddBaseListTypes(factoryInterface)
                .AddModifiers(SH.PublicToken());

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
            yield return GenerateLogoutRequestMessage(contract);
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
            yield return GenerateFactoryMethod("CreateLogoutRequestMessage", Names.LogoutRequestMessageInterface, contractInfo.LogoutRequestMessageClassName);
            yield return GenerateFactoryMethod("CreateHeartBeatMessage", Names.HeartbeatMessageInterface, contractInfo.HeartbeatMessageClassName);
            yield return GenerateFactoryMethod("CreateCancelRequestMessage", Names.CancelRequestMessageInterface, contractInfo.CancelRequestMessageClassName);
        }

        private static MethodDeclarationSyntax GenerateFactoryMethod(string methodName, TypeString retType, TypeString messageType)
        {
            var retStatement = SF.ReturnStatement(
                SF.ObjectCreationExpression(SH.FullTypeName(messageType))
                    .WithoutArguments());

            return SF.MethodDeclaration(SH.FullTypeName(retType), methodName)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddBodyStatements(retStatement);
        }

        private static ClassBuildNode GenerateMessageBase(ContractDeclaration contract)
        {
            var baseMessageClassName = contract.BaseMessageClassName;

            var msgDeclaration = SF.InterfaceDeclaration(baseMessageClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SF.SimpleBaseType(SH.GlobalTypeName(Names.MessageInterface)));

            return new ClassBuildNode(0, baseMessageClassName, msgDeclaration);
        }

        private static ClassBuildNode GenerateLoginMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.LoginMessageClassName;

            var msgBase = SF.SimpleBaseType(SH.FullTypeName(contractInfo.BaseMessageClassName));
            var iLoginBase = SF.SimpleBaseType(SH.FullTypeName(Names.LoginMessageInterface));

            var userNameProperty = SF
                .PropertyDeclaration(SF.ParseTypeName("string"), "UserName")
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var passwordProperty = SF
                .PropertyDeclaration(SF.ParseTypeName("string"), "Password")
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var resultPropertyType = SF.NullableType(SH.FullTypeName(Names.LoginResultEnum));
            var resultProperty = SF
                .PropertyDeclaration(resultPropertyType, "ResultCode")
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var errorMessageProperty = SF
                .PropertyDeclaration(SF.ParseTypeName("string"), "ErrorMessage")
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var messageClassDeclaration = SF.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iLoginBase);

            return new ClassBuildNode(LoginMessageKey, messageClassName, messageClassDeclaration)
                .AddAuxProperties(CreateSystemMessageNameProperty(contractInfo.LoginMessageClassName.Short))
                .AddProperties(userNameProperty, passwordProperty, resultProperty, errorMessageProperty);
        }

        private static ClassBuildNode GenerateLogoutMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.LogoutMessageClassName;

            var msgBase = SF.SimpleBaseType(SH.FullTypeName(contractInfo.BaseMessageClassName));
            var iLogoutBase = SF.SimpleBaseType(SH.FullTypeName(Names.LogoutMessageInterface));

            var messageClassDeclaration = SF.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iLogoutBase);

            return new ClassBuildNode(LogoutMessageKey, messageClassName, messageClassDeclaration)
                .AddAuxProperties(CreateSystemMessageNameProperty(contractInfo.LogoutMessageClassName.Short));
        }

        private static ClassBuildNode GenerateLogoutRequestMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.LogoutRequestMessageClassName;

            var msgBase = SF.SimpleBaseType(SH.FullTypeName(contractInfo.BaseMessageClassName));
            var iLogoutBase = SF.SimpleBaseType(SH.FullTypeName(Names.LogoutRequestMessageInterface));

            var messageClassDeclaration = SF.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iLogoutBase);

            return new ClassBuildNode(LogoutRequestMessageKey, messageClassName, messageClassDeclaration)
                .AddAuxProperties(CreateSystemMessageNameProperty(contractInfo.LogoutRequestMessageClassName.Short));
        }

        private static ClassBuildNode GenerateHeartbeatMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.HeartbeatMessageClassName;

            var msgBase = SF.SimpleBaseType(SH.FullTypeName(contractInfo.BaseMessageClassName));
            var iHeartbeatBase = SF.SimpleBaseType(SH.FullTypeName(Names.HeartbeatMessageInterface));

            var messageClassDeclaration = SF.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iHeartbeatBase);

            return new ClassBuildNode(HeartbeatMessageKey, messageClassName, messageClassDeclaration)
                .AddAuxProperties(CreateSystemMessageNameProperty(contractInfo.HeartbeatMessageClassName.Short));
        }

        private static ClassBuildNode GenerateCancelRequestMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.CancelRequestMessageClassName;

            var msgBase = SF.SimpleBaseType(SH.FullTypeName(contractInfo.BaseMessageClassName));
            var iCancelRequestBase = SF.SimpleBaseType(SH.FullTypeName(Names.CancelRequestMessageInterface));

            var callIdProperty = GenerateMessageProperty("string", "CallId");

            var messageClassDeclaration = SF.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
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

            baseTypes.Add(SF.SimpleBaseType(SH.ShortTypeName(ContractInfo.BaseMessageClassName)));

            if (MessageType == MessageType.Request)
            {
                if (RpcInfo.HasStreams)
                    baseTypes.Add(SF.SimpleBaseType(SH.GlobalTypeName(Names.StreamRequestInterface)));
                else
                    baseTypes.Add(SF.SimpleBaseType(SH.GlobalTypeName(Names.RequestInterface)));
            }
            else if (MessageType == MessageType.Response)
            {
                if (RpcInfo.ReturnsData)
                    baseTypes.Add(SF.SimpleBaseType(SH.GenericType(Names.ResponseInterface.Full, RpcInfo.ReturnParam.ParamType)));
                else
                    baseTypes.Add(SF.SimpleBaseType(SH.GlobalTypeName(Names.ResponseInterface)));
            }
            else if (MessageType == MessageType.Fault)
                baseTypes.Add(SF.SimpleBaseType(SH.FullTypeName(Names.FaultMessageInterface)));

            var messageClassDeclaration = SF.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
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
            return SF.PropertyDeclaration(SF.ParseTypeName(type), name)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();
        }

        private PropertyDeclarationSyntax GenerateMessageProperty(ParamDeclaration callProp, int index)
        {
            return SF.PropertyDeclaration(SF.ParseTypeName(callProp.ParamType), callProp.MessagePropertyName)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();
        }

        private MethodDeclarationSyntax GenerateGetCustomFaultMethod()
        {
            var hasCustomFaults = RpcInfo.CustomFaults.Count > 0;
            var faultDataRef = hasCustomFaults
                ? SF.IdentifierName("CustomFaultBinding")
                : (ExpressionSyntax)SH.NullLiteral();

            var retBindingPropStatement = SF.ReturnStatement(faultDataRef);

            var retType = SH.FullTypeName(Names.CustomFaultBindingInterface);

            return SF.MethodDeclaration(retType, "GetCustomFaultBinding")
                .AddModifiers(SH.PublicToken())
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

            var adapterInterfaceDeclaration = SF.InterfaceDeclaration(adapterInterfaceName.Short)
                .AddModifiers(SH.PublicToken())
                .AddBaseListTypes(SF.SimpleBaseType(SH.FullTypeName(Names.CustomFaultBindingInterface)))
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

                var getFaultReturnStatement = SF.ReturnStatement(SF.IdentifierName("Data"));
                var getFaultMethod = SF.MethodDeclaration(SF.IdentifierName("object"), "GetFault")
                    .AddModifiers(SH.PublicToken())
                    .AddBodyStatements(getFaultReturnStatement);

                var exceptionCreationStatemnt = SF.ReturnStatement(
                    SF.ObjectCreationExpression(SH.GenericType(Names.RpcFaultException.Full, dataType))
                    .AddArgumentListArguments(SH.IdentifierArgument("text"), SH.IdentifierArgument("Data")));

                var createExceprtionMethod = SF.MethodDeclaration(SH.FullTypeName(Names.RpcFaultException), "CreateException")
                    .AddModifiers(SH.PublicToken())
                    .AddParameterListParameters(SH.Parameter("text", "string"))
                    .AddBodyStatements(exceptionCreationStatemnt);

                var adapterClass = SF.ClassDeclaration(adapterClassName.Short)
                    .AddModifiers(SH.PublicToken())
                    .AddBaseListTypes(SF.SimpleBaseType(SF.IdentifierName(interfaceName)));

                var dataProp = GenerateMessageProperty(dataType, "Data");

                yield return new ClassBuildNode(key, adapterClassName, adapterClass)
                    .AddProperties(dataProp)
                    .AddMethods(getFaultMethod, createExceprtionMethod);
            }
        }

        private static ClassBuildNode GenerateStreamPageMessage(int messageKey, TypeString messageClassName, ContractDeclaration contractInfo, string streamType, OperationDeclaration opInfo)
        {
            //var messageClassName = contractInfo.GetStreamPageClassName(streamType);
            var genericListType = SH.GenericType("System.Collections.Generic.List", streamType);

            var streamIdParam = SH.Parameter("streamId", "string");
            var streamIdInitStatement = SH.AssignmentStatement(SF.IdentifierName("CallId"), SF.IdentifierName("streamId"));
            var constructor = SF.ConstructorDeclaration(messageClassName.Short)
                .AddModifiers(SH.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(streamIdInitStatement);

            var streamIdProperty = SF
                .PropertyDeclaration(SF.ParseTypeName("string"), "CallId")
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var itemsProperty = SF
                .PropertyDeclaration(genericListType, "Items")
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var msgBase = SF.SimpleBaseType(SH.FullTypeName(contractInfo.BaseMessageClassName));
            var iStreamBase = SF.SimpleBaseType(SH.GenericName(Names.StreamPageInterface.Full, streamType));

            var messageClassDeclaration = SF.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iStreamBase)
                .AddMembers(constructor);

            return new ClassBuildNode(messageKey, messageClassName, messageClassDeclaration)
                .AddAuxProperties(CreateContractMessageNameProperty(opInfo, "DataPage"))
                .AddProperties(streamIdProperty, itemsProperty);
        }

        private static ClassBuildNode GenerateStreamPageAckMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.StreamPageAckMessageClassName;

            var streamIdParam = SH.Parameter("streamId", "string");
            var streamIdInitStatement = SH.AssignmentStatement(SF.IdentifierName("CallId"), SF.IdentifierName("streamId"));
            var constructor = SF.ConstructorDeclaration(messageClassName.Short)
                .AddModifiers(SH.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(streamIdInitStatement);

            var streamIdProperty = SF
                .PropertyDeclaration(SF.ParseTypeName("string"), "CallId")
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var pagesConsumedProperty = SF
                .PropertyDeclaration(SF.ParseTypeName("ushort"), "Consumed")
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var msgBase = SF.SimpleBaseType(SH.FullTypeName(contractInfo.BaseMessageClassName));
            var iStreamAckBase = SF.SimpleBaseType(SH.FullTypeName(Names.StreamPageAckInterface));

            var messageClassDeclaration = SF.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iStreamAckBase)
                .AddMembers(constructor);

            return new ClassBuildNode(StreamAckMessageKey, messageClassName, messageClassDeclaration)
                .AddAuxProperties(CreateSystemMessageNameProperty("PageAcknowledgement"))
                .AddProperties(streamIdProperty, pagesConsumedProperty);
        }

        private static ClassBuildNode GenerateStreamCancelMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.StreamCancelMessageClassName;

            var streamIdParam = SH.Parameter("streamId", "string");
            var streamIdInitStatement = SH.AssignmentStatement(SF.IdentifierName("CallId"), SF.IdentifierName("streamId"));
            var constructor = SF.ConstructorDeclaration(messageClassName.Short)
                .AddModifiers(SH.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(streamIdInitStatement);

            var streamIdProperty = SF
                .PropertyDeclaration(SF.ParseTypeName("string"), "CallId")
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var optionsProperty = SF
                .PropertyDeclaration(SF.ParseTypeName("SharpRpc.StreamCancelOptions"), "Options")
                .AddModifiers(SH.PublicToken())
                .AddAutoGetter()
                .AddAutoSetter();

            var msgBase = SF.SimpleBaseType(SH.FullTypeName(contractInfo.BaseMessageClassName));
            var iStreamComplBase = SF.SimpleBaseType(SH.FullTypeName(Names.StreamCancelMessageInterface));

            var messageClassDeclaration = SF.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iStreamComplBase)
                .AddMembers(constructor);

            return new ClassBuildNode(StreamCancelMessageKey, messageClassName, messageClassDeclaration)
                .AddAuxProperties(CreateContractMessageNameProperty("CancelStream"))
                .AddProperties(streamIdProperty, optionsProperty);
        }

        private static ClassBuildNode GenerateStreamCloseMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.StreamCloseMessageClassName;

            var streamIdParam = SH.Parameter("streamId", "string");
            var streamIdInitStatement = SH.AssignmentStatement(SF.IdentifierName("CallId"), SF.IdentifierName("streamId"));
            var constructor = SF.ConstructorDeclaration(messageClassName.Short)
                .AddModifiers(SH.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(streamIdInitStatement);

            var streamIdProperty = SF
                .PropertyDeclaration(SF.ParseTypeName("string"), "CallId")
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var optionsProperty = SF
                .PropertyDeclaration(SF.ParseTypeName("SharpRpc.StreamCloseOptions"), "Options")
                .AddModifiers(SH.PublicToken())
                .AddAutoGetter()
                .AddAutoSetter();

            var msgBase = SF.SimpleBaseType(SH.FullTypeName(contractInfo.BaseMessageClassName));
            var iStreamComplBase = SF.SimpleBaseType(SH.FullTypeName(Names.StreamCloseMessageInterface));

            var messageClassDeclaration = SF.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iStreamComplBase)
                .AddMembers(constructor);

            return new ClassBuildNode(StreamCloseMessageKey, messageClassName, messageClassDeclaration)
                .AddAuxProperties(CreateContractMessageNameProperty("CloseStream"))
                .AddProperties(streamIdProperty, optionsProperty);
        }

        private static ClassBuildNode GenerateStreamCloseAckMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.StreamCloseAckMessageClassName;

            var streamIdParam = SH.Parameter("streamId", "string");
            var streamIdInitStatement = SH.AssignmentStatement(SF.IdentifierName("CallId"), SF.IdentifierName("streamId"));
            var constructor = SF.ConstructorDeclaration(messageClassName.Short)
                .AddModifiers(SH.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(streamIdInitStatement);

            var streamIdProperty = SF
                .PropertyDeclaration(SF.ParseTypeName("string"), "CallId")
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var msgBase = SF.SimpleBaseType(SH.FullTypeName(contractInfo.BaseMessageClassName));
            var iStreamComplBase = SF.SimpleBaseType(SH.FullTypeName(Names.StreamCloseAckMessageInterface));

            var messageClassDeclaration = SF.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
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
            var factoryInterface = SH.GenericName(Names.StreamFactoryInterface.Full, streamType);

            var streamIdParam = SH.Parameter("streamId", "string");

            // page factory method

            var pageCreationStatement = SF.ReturnStatement(
                SF.ObjectCreationExpression(SH.ShortTypeName(pageClassName))
                    .AddArgumentListArguments(SH.IdentifierArgument("streamId")));

            var createPageRetType = SH.GenericType(Names.StreamPageInterface.Full, streamType);
            var createPageMethod = SF.MethodDeclaration(createPageRetType, "CreatePage")
                .AddModifiers(SH.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(pageCreationStatement);

            // acknowledgement factory method

            var ackClassName = contractInfo.StreamPageAckMessageClassName;
            var ackCreationStatement = SF.ReturnStatement(
                SF.ObjectCreationExpression(SH.ShortTypeName(ackClassName))
                    .AddArgumentListArguments(SH.IdentifierArgument("streamId")));

            var createAckRetType = SF.IdentifierName(Names.StreamPageAckInterface.Full);
            var createAckMethod = SF.MethodDeclaration(createAckRetType, "CreatePageAcknowledgement")
                .AddModifiers(SH.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(ackCreationStatement);

            // close message factory method

            var closeMessageType = SH.ShortTypeName(contractInfo.StreamCloseMessageClassName);
            var closeMessageCreationStatement = SF.ReturnStatement(
                SF.ObjectCreationExpression(closeMessageType)
                    .AddArgumentListArguments(SH.IdentifierArgument("streamId")));

            var createCloseMsgRetType = SF.IdentifierName(Names.StreamCloseMessageInterface.Full);
            var createCloseMsgMethod = SF.MethodDeclaration(createCloseMsgRetType, "CreateCloseMessage")
                .AddModifiers(SH.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(closeMessageCreationStatement);

            // close ack message factory method

            var closeAckMessageType = SH.ShortTypeName(contractInfo.StreamCloseAckMessageClassName);
            var closeAckMessageCreationStatement = SF.ReturnStatement(
                SF.ObjectCreationExpression(closeAckMessageType)
                    .AddArgumentListArguments(SH.IdentifierArgument("streamId")));

            var createCloseAckMsgRetType = SF.IdentifierName(Names.StreamCloseAckMessageInterface.Full);
            var createCloseAckMsgMethod = SF.MethodDeclaration(createCloseAckMsgRetType, "CreateCloseAcknowledgement")
                .AddModifiers(SH.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(closeAckMessageCreationStatement);

            // cancel message factory method

            var cancelRequestMessageType = SH.ShortTypeName(contractInfo.StreamCancelMessageClassName);
            var cancelMessageCreationStatement = SF.ReturnStatement(
                SF.ObjectCreationExpression(cancelRequestMessageType)
                    .AddArgumentListArguments(SH.IdentifierArgument("streamId")));

            var createCancelMsgRetType = SF.IdentifierName(Names.StreamCancelMessageInterface.Full);
            var createCancelMsgMethod = SF.MethodDeclaration(createCancelMsgRetType, "CreateCancelMessage")
                .AddModifiers(SH.PublicToken())
                .AddParameterListParameters(streamIdParam)
                .AddBodyStatements(cancelMessageCreationStatement);

            // class declaration

            var classDec = SF.ClassDeclaration(factoryClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SF.SimpleBaseType(factoryInterface))
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
            return SH.CreateLambdaGetOnlyProperty("string", "ContractMessageName", SH.LiteralExpression(name))
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword));
        }
    }
}
