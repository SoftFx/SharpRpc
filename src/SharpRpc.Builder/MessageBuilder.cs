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

namespace SharpRpc.Builder
{
    public enum MessageType
    {
        OneWay,
        Request,
        Response,
        System
    }

    internal class MessageBuilder
    {
        internal MessageBuilder(ContractDeclaration contract, CallDeclaration callDec, MessageType type)
        {
            ContractInfo = contract;
            RpcInfo = callDec;
            MessageType = type;
        }

        public CallDeclaration RpcInfo { get; }
        public ContractDeclaration ContractInfo { get; }
        public MessageType MessageType { get; }

        public static IEnumerable<ClassBuildNode> GenerateSystemMessages(ContractDeclaration contract)
        {
            yield return GenerateLoginMessage(contract);
            yield return GenerateLogoutMessage(contract);
            yield return GenerateHeartbeatMessage(contract);

            foreach (var faultMsg in GenerateFaultMessages(contract))
                yield return faultMsg;
        }

        public static IEnumerable<ClassBuildNode> GenerateFaultMessages(ContractDeclaration contract)
        {
            yield return GenerateFaultMessage(contract);

            foreach (var customFault in contract.FaultTypes)
                yield return GenerateCustomFaultMessage(contract, customFault);
        }

        public static IEnumerable<ClassBuildNode> GenerateStreamMessages(ContractDeclaration contract)
        {
            yield return GenerateStreamAcknowledgementMessage(contract);
            yield return GenerateStreamCompletionMessage(contract);

            foreach (var streamType in contract.StreamTypes)
                yield return GenerateStreamPageMessage(contract, streamType);
        }

        public static IEnumerable<ClassDeclarationSyntax> GenerateStreamFactories(ContractDeclaration contract)
        {
            foreach (var streamType in contract.StreamTypes)
                yield return GenerateStreamFactory(contract, streamType);
        }

        public static IEnumerable<ClassBuildNode> GenerateUserMessages(ContractDeclaration contract, GeneratorExecutionContext context, MetadataDiagnostics diagnostics)
        {
            foreach (var call in contract.Calls)
            {
                if (call.CallType == ContractCallType.CallToServer || call.CallType == ContractCallType.CallToClient)
                {
                    yield return new MessageBuilder(contract, call, MessageType.Request).GenerateMessage(context, true, Names.RequestClassPostfix);
                    yield return new MessageBuilder(contract, call, MessageType.Response).GenerateMessage(context, false, Names.ResponseClassPostfix);
                }
                else
                {
                    if (call.ReturnsData)
                        diagnostics.AddOneWayReturnsDataWarning(call.CodeLocation, call.MethodName);

                    yield return new MessageBuilder(contract, call, MessageType.OneWay).GenerateMessage(context, true, Names.MessageClassPostfix);
                }
            }
        }

        public static ClassDeclarationSyntax GenerateFactory(ContractDeclaration contractInfo)
        {
            var factoryInterface = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.MessageFactoryInterface));

            var loginMsgMethod = GenerateFactoryMethod("CreateLoginMessage", Names.LoginMessageInterface, contractInfo.LoginMessageClassName);
            var logoutMsgMethod = GenerateFactoryMethod("CreateLogoutMessage", Names.LogoutMessageInterface, contractInfo.LogoutMessageClassName);
            var heartbeatMsgMethod = GenerateFactoryMethod("CreateHeartBeatMessage", Names.HeartbeatMessageInterface, contractInfo.HeartbeatMessageClassName);
            var faultFactoryMethod = GenerateFaultFactory(contractInfo);
            var customFaultsMethod = GenerateCustomFaultFactory(contractInfo);
            //var basicAuthMethod = GenerateFactoryMethod("CreateBasicAuthData", Names.BasicAuthDataInterface, contractInfo.BasicAuthDataClassName);

            return SyntaxFactory.ClassDeclaration(contractInfo.MessageFactoryClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
                .AddBaseListTypes(factoryInterface)
                .AddMembers(loginMsgMethod, logoutMsgMethod, heartbeatMsgMethod, faultFactoryMethod, customFaultsMethod);
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

        private static MethodDeclarationSyntax GenerateFaultFactory(ContractDeclaration contract)
        {
            var messageCreation = SyntaxFactory.ObjectCreationExpression(SyntaxHelper.FullTypeName(contract.FaultMessageClassName))
                        .WithoutArguments();

            var retStatement = SyntaxFactory.ReturnStatement(messageCreation);
            var retType = SyntaxHelper.FullTypeName(Names.FaultMessageInterface);

            return SyntaxFactory.MethodDeclaration(retType, "CreateFaultMessage")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBodyStatements(retStatement);
        }

        private static MethodDeclarationSyntax GenerateCustomFaultFactory(ContractDeclaration contract)
        {
            var cases = new List<SwitchSectionSyntax>();
            var retType = SyntaxHelper.GenericType(Names.FaultMessageInterface.Full, "T");

            foreach (var faultDataType in contract.FaultTypes)
            {
                var faultMessageType = contract.GetCustomFaultMessageClassName(faultDataType);
                var messageCreation = SyntaxFactory.ObjectCreationExpression(SyntaxHelper.FullTypeName(faultMessageType))
                        .WithoutArguments();

                var retStatement = SyntaxFactory.ReturnStatement(
                    SyntaxFactory.CastExpression(retType, messageCreation));

                var label = contract.Compatibility.SupportsPatternMatching
                    ? SyntaxFactory.CaseSwitchLabel(SyntaxFactory.IdentifierName(faultDataType))
                    : SyntaxFactory.CaseSwitchLabel(SyntaxHelper.LiteralExpression(faultDataType));

                cases.Add(SyntaxFactory.SwitchSection()
                    .AddLabels(label)
                    .AddStatements(retStatement));
            }

            var exceptionCreationExp = SyntaxFactory.ObjectCreationExpression(
                SyntaxFactory.IdentifierName(Names.SystemException))
                .WithoutArguments();

            cases.Add(SyntaxFactory.SwitchSection()
                .AddLabels(SyntaxFactory.DefaultSwitchLabel())
                .AddStatements(SyntaxFactory.ThrowStatement(exceptionCreationExp)));

            var switchArg = contract.Compatibility.SupportsPatternMatching
                ? (ExpressionSyntax)SyntaxFactory.IdentifierName("fault")
                : SyntaxHelper.MemberOf(SyntaxFactory.TypeOfExpression(SyntaxFactory.IdentifierName("T")), "FullName");

            var switchStatement = SyntaxFactory.SwitchStatement(switchArg)
                .AddSections(cases.ToArray());

            var typeConstraint = SyntaxFactory.TypeParameterConstraintClause("T")
                .AddConstraints(SyntaxFactory.TypeConstraint(SyntaxHelper.FullTypeName(Names.BasicRpcFault)));

            return SyntaxFactory.MethodDeclaration(retType, "CreateFaultMessage")
                .AddParameterListParameters(SyntaxHelper.Parameter("fault", "T"))
                .AddTypeParameterListParameters(SyntaxFactory.TypeParameter("T"))
                .AddConstraintClauses(typeConstraint)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBodyStatements(switchStatement);
        }

        public static ClassBuildNode GenerateMessageBase(ContractDeclaration contract)
        {
            var baseMessageClassName = contract.BaseMessageClassName;

            //var clasDeclaration = SyntaxFactory.ClassDeclaration(baseMessageClassName.Short)
            //    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.AbstractKeyword))
            //    .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxHelper.GlobalTypeName(Names.MessageInterface)));

            var msgDeclaration = SyntaxFactory.InterfaceDeclaration(baseMessageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxHelper.GlobalTypeName(Names.MessageInterface)));

            return new ClassBuildNode(baseMessageClassName, msgDeclaration);
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

            //var authDataProperty = SyntaxFactory
            //    .PropertyDeclaration(SyntaxFactory.ParseTypeName("AuthData"), "AuthData")
            //    .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            //    .AddAutoGetter()
            //    .AddAutoSetter();

            //var authImplicitGetter = SyntaxFactory
            //    .AccessorDeclaration(SyntaxKind.GetAccessorDeclaration,
            //        SyntaxFactory.Block(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("AuthData"))));

            //var authDataCast = SyntaxFactory.CastExpression(SyntaxHelper.ShortTypeName(contractInfo.AuthDataClassName),
            //    SyntaxFactory.IdentifierName("value"));

            //var authImplicitSetter = SyntaxFactory
            //    .AccessorDeclaration(SyntaxKind.SetAccessorDeclaration,
            //        SyntaxFactory.Block(SyntaxFactory.ExpressionStatement(
            //            SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName("AuthData"), authDataCast))));

            //var authImplicitProperty = SyntaxFactory
            //    .PropertyDeclaration(SyntaxHelper.FullTypeName(Names.AuthDataInterface), Names.LoginMessageInterface.Full + ".AuthData")
            //    .AddAccessorListAccessors(authImplicitGetter, authImplicitSetter);

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iLoginBase);

            return new ClassBuildNode(messageClassName, messageClassDeclaration, userNameProperty, passwordProperty, resultProperty, errorMessageProperty);
        }

        private static ClassBuildNode GenerateLogoutMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.LogoutMessageClassName;

            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(contractInfo.BaseMessageClassName));
            var iLogoutBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.LogoutMessageInterface));

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iLogoutBase);

            return new ClassBuildNode(messageClassName, messageClassDeclaration);
        }

        private static ClassBuildNode GenerateHeartbeatMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.HeartbeatMessageClassName;

            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(contractInfo.BaseMessageClassName));
            var iHeartbeatBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.HeartbeatMessageInterface));

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iHeartbeatBase);

            return new ClassBuildNode(messageClassName, messageClassDeclaration, new List<PropertyDeclarationSyntax>());
        }

        internal ClassBuildNode GenerateMessage(GeneratorExecutionContext context, bool direct, string namePostfix)
        {
            var messageClassName = ContractInfo.GetMessageClassName(RpcInfo.MethodName, namePostfix);

            var baseTypes = new List<BaseTypeSyntax>();
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

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(baseTypes.ToArray());

            var properties = new List<PropertyDeclarationSyntax>();

            if (MessageType == MessageType.Request || MessageType == MessageType.Response)
            {
                properties.Add(GenerateMessageProperty("string", "CallId"));
            }

            if (direct)
            {
                var index = 1;

                foreach (var param in RpcInfo.Params)
                    properties.Add(GenerateMessageProperty(param, index++));
            }
            else
            {
                if (RpcInfo.ReturnsData)
                    properties.Add(GenerateMessageProperty(RpcInfo.ReturnParam.ParamType, Names.ResponseResultProperty));
            }

            return new ClassBuildNode(messageClassName, messageClassDeclaration, properties);
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

        private static ClassBuildNode GenerateFaultMessage(ContractDeclaration contractInfo)
        {
            var messageClassName = contractInfo.FaultMessageClassName;

            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(contractInfo.BaseMessageClassName));
            var iFaultBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.FaultMessageInterface));

            GenerateCommonFaultProperties(out var idProperty, out var textProperty, out var codeProperty);

            var exceptionCreation = SyntaxFactory.ObjectCreationExpression(SyntaxHelper.FullTypeName(Names.RpcFaultException))
                .AddArgumentListArguments(SyntaxHelper.IdentifierArgument("Code"), SyntaxHelper.IdentifierArgument("Text"));

            var exceptionMethod = SyntaxFactory.MethodDeclaration(SyntaxHelper.FullTypeName(Names.RpcFaultException), "CreateException")
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddBodyStatements(SyntaxFactory.ReturnStatement(exceptionCreation));

            var faultCreation = SyntaxFactory.ObjectCreationExpression(SyntaxFactory.IdentifierName("SharpRpc.RpcFaultStub"))
                .AddArgumentListArguments(SyntaxHelper.IdentifierArgument("Code"), SyntaxHelper.IdentifierArgument("Text"));

            var getFaultMethod = SyntaxFactory.MethodDeclaration(SyntaxHelper.FullTypeName(Names.BasicRpcFault), "GetFault")
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddBodyStatements(SyntaxFactory.ReturnStatement(faultCreation));

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddMembers(exceptionMethod, getFaultMethod)
                .AddBaseListTypes(msgBase, iFaultBase);

            return new ClassBuildNode(messageClassName, messageClassDeclaration, idProperty, textProperty, codeProperty);
        }

        private static ClassBuildNode GenerateCustomFaultMessage(ContractDeclaration contractInfo, string faultDataType)
        {
            var messageClassName = contractInfo.GetCustomFaultMessageClassName(faultDataType);

            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(contractInfo.BaseMessageClassName));
            var iFaultBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.GenericType(Names.FaultMessageInterface.Full, faultDataType));

            GenerateCommonFaultProperties(out var idProperty, out var textProperty, out var codeProperty);

            var dataProperty = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName(faultDataType), "FaultData")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            var exceptionType = SyntaxHelper.GenericType(Names.RpcFaultException.Full, SyntaxFactory.ParseTypeName(faultDataType));

            var exceptionCreation = SyntaxFactory.ObjectCreationExpression(exceptionType)
                .AddArgumentListArguments(SyntaxHelper.IdentifierArgument("FaultData"));

            var exceptionMethod = SyntaxFactory.MethodDeclaration(SyntaxHelper.FullTypeName(Names.RpcFaultException), "CreateException")
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddBodyStatements(SyntaxFactory.ReturnStatement(exceptionCreation));

            var getFaultMethod = SyntaxFactory.MethodDeclaration(SyntaxHelper.FullTypeName(Names.BasicRpcFault), "GetFault")
                .AddModifiers(SyntaxHelper.PublicToken())
                .AddBodyStatements(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("FaultData")));

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddMembers(exceptionMethod, getFaultMethod)
                .AddBaseListTypes(msgBase, iFaultBase);

            return new ClassBuildNode(messageClassName, messageClassDeclaration, idProperty, textProperty, codeProperty, dataProperty);
        }

        private static void GenerateCommonFaultProperties(out PropertyDeclarationSyntax idProperty,
            out PropertyDeclarationSyntax textProperty, out PropertyDeclarationSyntax codeProperty)
        {
            idProperty = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName("string"), "CallId")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            textProperty = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName("string"), "Text")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();

            codeProperty = SyntaxFactory
                .PropertyDeclaration(SyntaxFactory.ParseTypeName("SharpRpc.RequestFaultCode"), "Code")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();
        }

        private static ClassBuildNode GenerateStreamPageMessage(ContractDeclaration contractInfo, string streamType)
        {
            var messageClassName = contractInfo.GetStreamPageClassName(streamType);
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

            //var listBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.GenericType("System.Collections.Generic.List", streamType));
            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(contractInfo.BaseMessageClassName));
            var iStreamBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.GenericName(Names.StreamPageInterface.Full, streamType));

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iStreamBase)
                .AddMembers(constructor);

            return new ClassBuildNode(messageClassName, messageClassDeclaration, streamIdProperty, itemsProperty);
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

            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(contractInfo.BaseMessageClassName));
            var iStreamAckBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.StreamPageAckInterface));

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iStreamAckBase)
                .AddMembers(constructor);

            return new ClassBuildNode(messageClassName, messageClassDeclaration, streamIdProperty);
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

            return new ClassBuildNode(messageClassName, messageClassDeclaration, streamIdProperty);
        }

        private static ClassDeclarationSyntax GenerateStreamFactory(ContractDeclaration contractInfo, string streamType)
        {
            var factoryClassName = contractInfo.GetStreamFactoryClassName(streamType);
            var factoryInterface = SyntaxHelper.GenericName(Names.StreamFactoryInterface.Full, streamType);

            var streamIdParam = SyntaxHelper.Parameter("streamId", "string");

            // page factory method

            var pageClassName = contractInfo.GetStreamPageClassName(streamType);
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

            return SyntaxFactory.ClassDeclaration(factoryClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SyntaxFactory.SimpleBaseType(factoryInterface))
                .AddMembers(createPageMethod, createCompletionMethod, createAckMethod);
        }
    }
}
