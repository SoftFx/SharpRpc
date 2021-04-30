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

    public class MessageBuilder
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

        internal static IEnumerable<ClassBuildNode> GenerateSystemMessages(ContractDeclaration contract)
        {
            yield return GenerateLoginMessage(contract);
            yield return GenerateLogoutMessage(contract);
            yield return GenerateHeartbeatMessage(contract);
        }

        //internal static IEnumerable<ClassBuildNode> GenerateAuthContracts(ContractDeclaration contract)
        //{
        //    yield return GenerateBasicAuthData(contract);
        //}

        internal static IEnumerable<ClassBuildNode> GenerateUserMessages(ContractDeclaration contract, GeneratorExecutionContext context)
        {
            foreach (var call in contract.Calls)
            {
                if (call.CallType == ContractCallType.ClientCall || call.CallType == ContractCallType.ServerCall)
                {
                    yield return new MessageBuilder(contract, call, MessageType.Request).GenerateMessage(context, true, Names.RequestClassPostfix);
                    yield return new MessageBuilder(contract, call, MessageType.Response).GenerateMessage(context, false, Names.ResponseClassPostfix);
                }
                else
                    yield return new MessageBuilder(contract, call, MessageType.OneWay).GenerateMessage(context, true, Names.MessageClassPostfix);
            }
        }

        internal static ClassDeclarationSyntax GenerateFactory(ContractDeclaration contractInfo)
        {
            var factoryInterface = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.MessageFactoryInterface));

            var loginMsgMethod = GenerateFactoryMethod("CreateLoginMessage", Names.LoginMessageInterface, contractInfo.LoginMessageClassName);
            var logoutMsgMethod = GenerateFactoryMethod("CreateLogoutMessage", Names.LogoutMessageInterface, contractInfo.LogoutMessageClassName);
            var heartbeatMsgMethod = GenerateFactoryMethod("CreateHeartBeatMessage", Names.HeartbeatMessageInterface, contractInfo.HeartbeatMessageClassName);
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

        public static ClassBuildNode GenerateMessageBase(ContractDeclaration contract)
        {
            var baseMessageClassName = contract.BaseMessageClassName;

            var clasDeclaration = SyntaxFactory.ClassDeclaration(baseMessageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.AbstractKeyword))
                .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxHelper.GlobalTypeName(Names.MessageInterface)));

            return new ClassBuildNode(baseMessageClassName, clasDeclaration);
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

        //public static ClassBuildNode GenerateBaseAuthData(ContractDeclaration contractInfo)
        //{
        //    var messageClassName = new TypeString("AuthData");

        //    var contractInterface = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.AuthDataInterface));

        //    var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
        //        .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
        //        .AddBaseListTypes(contractInterface);

        //    return new ClassBuildNode(messageClassName, messageClassDeclaration);
        //}

        //private static ClassBuildNode GenerateBasicAuthData(ContractDeclaration contractInfo)
        //{
        //    var messageClassName = contractInfo.BasicAuthDataClassName;

        //    var baseClass = SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("AuthData"));
        //    var contractInterface = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.BasicAuthDataInterface));

        //    var userNameProperty = SyntaxFactory
        //        .PropertyDeclaration(SyntaxFactory.ParseTypeName("string"), "UserName")
        //        .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
        //        .AddAutoGetter()
        //        .AddAutoSetter();

        //    var passwordProperty = SyntaxFactory
        //        .PropertyDeclaration(SyntaxFactory.ParseTypeName("string"), "Password")
        //        .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
        //        .AddAutoGetter()
        //        .AddAutoSetter();

        //    var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageClassName.Short)
        //        .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
        //        .AddBaseListTypes(baseClass, contractInterface);

        //    return new ClassBuildNode(messageClassName, messageClassDeclaration, userNameProperty, passwordProperty);
        //}

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
                baseTypes.Add(SyntaxFactory.SimpleBaseType(SyntaxHelper.GlobalTypeName(Names.RequestInterface)));
            else if (MessageType == MessageType.Response)
            {
                if (RpcInfo.ReturnsData)
                    baseTypes.Add(SyntaxFactory.SimpleBaseType(SyntaxHelper.GenericType(Names.RequestInterface.Full, RpcInfo.ReturnParam.ParamType)));
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
    }
}
