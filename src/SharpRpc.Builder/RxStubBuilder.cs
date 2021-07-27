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
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using SH = SharpRpc.Builder.SyntaxHelper;

namespace SharpRpc.Builder
{
    internal class RxStubBuilder
    {
        private readonly ContractDeclaration _contract;
        private readonly bool _isCallbackStub;

        public RxStubBuilder(ContractDeclaration contract, bool isCallback)
        {
            _contract = contract;
            _isCallbackStub = isCallback;
        }

        public ClassDeclarationSyntax GenerateServiceBase()
        {
            var serverStubType = _isCallbackStub ? _contract.CallbackServiceStubClassName : _contract.ServiceStubClassName;

            var stubClass = SF.ClassDeclaration(serverStubType.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword), SF.Token(SyntaxKind.AbstractKeyword))
                .AddMembers(GenerateStubMethods(serverStubType));

            if (!_isCallbackStub)
            {
                stubClass = stubClass.AddMembers(GenerateSessionProperty());

                if (_contract.HasCallbacks)
                    stubClass = stubClass.AddMembers(GenerateClientStubProperty());

                stubClass = stubClass.AddMembers(GenerateOnInitMethod(), GenerateStubInitServiceStubMethod());
            }

            return stubClass;
        }

        public ClassDeclarationSyntax GenerateHandler()
        {
            var handlerClassName = _isCallbackStub ? _contract.CallbackHandlerClassName : _contract.ServiceHandlerClassName;
            var serverStubClassName = _isCallbackStub ? _contract.CallbackServiceStubClassName : _contract.ServiceStubClassName;

            var serviceImplField = SH.FieldDeclaration("_stub", SF.IdentifierName(serverStubClassName.Short));

            var constructorBody = SF.Block(
                SH.AssignmentStatement(SF.IdentifierName("_stub"), SF.IdentifierName("serviceImpl")));

            var serviceImplParam = SH.Parameter("serviceImpl", serverStubClassName.Short);

            var constructor = SF.ConstructorDeclaration(handlerClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(serviceImplParam)
                .WithBody(constructorBody);

            var handlerClass = SF.ClassDeclaration(handlerClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PrivateKeyword))
                .AddBaseListTypes(SF.SimpleBaseType(SF.ParseTypeName(Names.RpcCallHandlerClass.Full)))
                .AddMembers(serviceImplField, constructor)
                .AddMembers(GenerateWrapMethods(handlerClassName))
                .AddMembers(GenerateOnMessageOverride(), GenerateOnRequestOverride());

            if (!_isCallbackStub)
                handlerClass = handlerClass.AddMembers(GenerateHandlerOnInitOverride());

            return handlerClass;
        }

        public MethodDeclarationSyntax GenerateBindMethod()
        {
            var serializerCreateClause = SH.InvocationExpression(Names.FacadeSerializerAdapterFactoryMethod, SF.Argument(SF.IdentifierName("serializer")));
            var serializerVarStatement = SH.VarDeclaration("adapter", serializerCreateClause);

            var msgFactoryVarStatement = SH.VarDeclaration("sFactory",
                SF.ObjectCreationExpression(SH.ShortTypeName(_contract.MessageFactoryClassName))
                .WithoutArguments());

            var serviceFactoryFunc = SH.GenericType("System.Func", _contract.ServiceStubClassName.Short);
            var serviceFactoryParam = SH.Parameter("serviceImplFactory", serviceFactoryFunc);

            var handlerCreationExp = SF.ObjectCreationExpression(SH.ShortTypeName(_contract.ServiceHandlerClassName))
                .AddArgumentListArguments(SF.Argument(SF.InvocationExpression(SF.IdentifierName("serviceImplFactory"))));

            var handlerCreationLambda = SF.ParenthesizedLambdaExpression()
                .WithExpressionBody(handlerCreationExp);

            //var handlerFactoryStatement = SH.VarDeclaration("handlerFactory",
            //    SF.ParenthesizedLambdaExpression()
            //    .WithExpressionBody(handlerCreationExp));

            var retStatement = SF.ReturnStatement(
                SF.ObjectCreationExpression(SH.FullTypeName(Names.ServiceBindingClass))
                .AddArgumentListArguments(
                    SF.Argument(handlerCreationLambda),
                    SH.IdentifierArgument("adapter"),
                    SH.IdentifierArgument("sFactory")));

            var serializerDefValue = SH.EnumValue(Names.SerializerChoiceEnum.Full, _contract.GetDefaultSerializerChoice());
            var serializerParam = SH.Parameter("serializer", Names.SerializerChoiceEnum.Full)
                .WithDefault(SF.EqualsValueClause(serializerDefValue));

            return SF.MethodDeclaration(SH.FullTypeName(Names.ServiceBindingClass), "CreateBinding")
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword), SF.Token(SyntaxKind.StaticKeyword))
                .AddParameterListParameters(serviceFactoryParam, serializerParam)
                .WithBody(SF.Block(serializerVarStatement, msgFactoryVarStatement, retStatement));
        }

        private MethodDeclarationSyntax[] GenerateStubMethods(TypeString clientStubTypeName)
        {
            var methods = new List<MethodDeclarationSyntax>();

            foreach (var callDec in GetAffectedCalls())
                methods.Add(GenerateStubMethod(callDec));

            return methods.ToArray();
        }

        private MethodDeclarationSyntax[] GenerateWrapMethods(TypeString clientStubTypeName)
        {
            var methods = new List<MethodDeclarationSyntax>();

            foreach (var callDec in GetAffectedCalls())
            {
                if (callDec.IsRequestResponceCall)
                {
                    methods.Add(GenerateRequestWrapMethod(callDec));
                    //methods.Add(GenerateOnRequestFailMethod(callDec));
                }
            }

            return methods.ToArray();
        }

        private MethodDeclarationSyntax GenerateStubMethod(CallDeclaration callDec)
        {
            var methodParams = new List<ParameterSyntax>();

            foreach (var param in callDec.Params)
            {
                var paramSyntax = SF
                    .Parameter(SF.Identifier(param.ParamName))
                    .WithType(GetTypeSyntax(param));

                methodParams.Add(paramSyntax);
            }

            var methodName = callDec.MethodName;
            var retType = GetValueTaskOf(callDec.ReturnParam);

            var method = SF.MethodDeclaration(retType, methodName)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword), SF.Token(SyntaxKind.AbstractKeyword))
                .AddParameterListParameters(methodParams.ToArray())
                .WithoutBody();

            return method;
        }

        private MethodDeclarationSyntax GenerateRequestWrapMethod(CallDeclaration callDec)
        {
            var methodName = "Invoke" + callDec.MethodName;
            var requetsMessageType = _contract.GetRequestClassName(callDec.MethodName);

            var handlerInvokationExp = SF.AwaitExpression(GenerateRpcInvocation(callDec, "request"));

            StatementSyntax handlerInvokationStatement;

            if (callDec.ReturnsData)
                handlerInvokationStatement = SH.VarDeclaration("result", handlerInvokationExp);
            else
                handlerInvokationStatement = SF.ExpressionStatement(handlerInvokationExp);

            var respStatements = GenerateResponseCreationStatements(callDec).ToArray();

            var tryCtachBlock = SF.TryStatement()
                .AddCatches(GenerateCustomCatches(callDec).ToArray())
                .AddCatches(GenerateRegularCatch(), GenerateUnexpectedCatch())
                .WithBlock(SF.Block(handlerInvokationStatement).AddStatements(respStatements));

            var retType = SH.GenericType(_contract.Compatibility.GetAsyncWrapper(), Names.ResponseInterface.Full);

            return SF.MethodDeclaration(retType, methodName)
               .AddModifiers(SF.Token(SyntaxKind.PrivateKeyword), SF.Token(SyntaxKind.AsyncKeyword))
               .AddParameterListParameters(SH.Parameter("request", requetsMessageType.Full))
               .WithBody(SF.Block(tryCtachBlock));
        }

        private MethodDeclarationSyntax GenerateOnMessageOverride()
        {
            StatementSyntax ifRoot = SF.ReturnStatement(
                SH.ThisInvocation(Names.RpcServiceBaseOnUnknownMessage, SH.IdentifierArgument("message")));

            var index = 0;

            foreach (var call in GetAffectedCalls())
            {
                if (call.IsOneWay)
                {
                    var messageType = _contract.GetOnWayMessageClassName(call.MethodName);
                    var typedMessageVarName = "m" + index++;
                    var rpcMethodCall = SF.ReturnStatement(GenerateRpcInvocation(call, typedMessageVarName));

                    ExpressionSyntax ifExpression;
                    StatementSyntax ifBody;

                    if (_contract.Compatibility.SupportsPatternMatching)
                    {
                        ifExpression = SF.IsPatternExpression(SF.IdentifierName("message"),
                            SF.DeclarationPattern(SF.ParseTypeName(messageType.Full), SF.SingleVariableDesignation(SF.Identifier(typedMessageVarName))));

                        ifBody = rpcMethodCall;
                    }
                    else
                    {
                        ifExpression = SF.BinaryExpression(SyntaxKind.IsExpression,
                            SF.IdentifierName("message"), SF.ParseTypeName(messageType.Full));

                        var castVariable = SH.VarDeclaration(typedMessageVarName,
                            SF.CastExpression(SF.ParseTypeName(messageType.Full), SF.IdentifierName("message")));

                        ifBody = SF.Block(castVariable, rpcMethodCall);
                    }

                    ifRoot = SF.IfStatement(ifExpression, ifBody, SF.ElseClause(ifRoot));
                }
            }

            var method = SF.MethodDeclaration(SF.ParseTypeName(_contract.Compatibility.GetAsyncWrapper()), Names.RpcServiceBaseOnMessageMethod)
               .AddModifiers(SF.Token(SyntaxKind.ProtectedKeyword), SF.Token(SyntaxKind.OverrideKeyword))
               .AddParameterListParameters(SH.Parameter("message", Names.MessageInterface.Full))
               .WithBody(SF.Block(ifRoot));

            return method;
        }

        private MethodDeclarationSyntax GenerateOnRequestOverride()
        {
            StatementSyntax ifRoot = SF.ReturnStatement(
                SH.ThisInvocation(Names.RpcServiceBaseOnUnknownRequest, SH.IdentifierArgument("request")));

            var index = 0;

            foreach (var call in GetAffectedCalls())
            {
                if (call.IsRequestResponceCall)
                {
                    var messageType = _contract.GetRequestClassName(call.MethodName);
                    var typedMessageVarName = "r" + index++;
                    var methodToInvoke = "Invoke" + call.MethodName;

                    var rpcMethodCall = SF.ReturnStatement(
                        SF.InvocationExpression(SF.IdentifierName(methodToInvoke))
                        .WithArguments(SH.IdentifierArgument(typedMessageVarName)));

                    ExpressionSyntax ifExpression;
                    StatementSyntax ifBody;

                    if (_contract.Compatibility.SupportsPatternMatching)
                    {
                        ifExpression = SF.IsPatternExpression(SF.IdentifierName("request"),
                            SF.DeclarationPattern(SF.ParseTypeName(messageType.Full), SF.SingleVariableDesignation(SF.Identifier(typedMessageVarName))));

                        ifBody = rpcMethodCall;
                    }
                    else
                    {
                        ifExpression = SF.BinaryExpression(SyntaxKind.IsExpression,
                               SF.IdentifierName("request"), SF.ParseTypeName(messageType.Full));

                        var castVariable = SH.VarDeclaration(typedMessageVarName,
                            SF.CastExpression(SF.ParseTypeName(messageType.Full), SF.IdentifierName("request")));

                        ifBody = SF.Block(castVariable, rpcMethodCall);
                    }

                    ifRoot = SF.IfStatement(ifExpression, ifBody, SF.ElseClause(ifRoot));
                }
            }

            var retType = SH.GenericType(_contract.Compatibility.GetAsyncWrapper(), Names.ResponseInterface.Full);

            return SF.MethodDeclaration(retType, Names.RpcServiceBaseOnRequestMethod)
               .AddModifiers(SF.Token(SyntaxKind.ProtectedKeyword), SF.Token(SyntaxKind.OverrideKeyword))
               .AddParameterListParameters(SH.Parameter("request", Names.RequestInterface.Full))
               .WithBody(SF.Block(ifRoot));
        }

        private ExpressionSyntax GenerateRpcInvocation(CallDeclaration callDec, string typedMessageVarName)
        {
            var args = new List<ArgumentSyntax>();

            foreach (var param in callDec.Params)
            {
                args.Add(SF.Argument(
                    SF.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    SF.IdentifierName(typedMessageVarName),
                    SF.IdentifierName(param.MessagePropertyName))));
            }

            var methodName = callDec.MethodName;

            //return SF.InvocationExpression(SF.IdentifierName(methodName), SH.CallArguments(args));
            return SF.InvocationExpression(SH.MemberOf(SF.IdentifierName("_stub"), methodName), SH.CallArguments(args));
        }

        private IEnumerable<StatementSyntax> GenerateResponseCreationStatements(CallDeclaration callDec)
        {
            var responseClassName = _contract.GetResponseClassName(callDec.MethodName);
            var respomseCreationExp = SF.ObjectCreationExpression(SF.ParseTypeName(responseClassName.Full)).WithoutArguments();

            yield return SH.VarDeclaration("response", respomseCreationExp);

            if (callDec.ReturnsData)
            {
                yield return SF.ExpressionStatement(
                    SF.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                        SH.MemeberOfIdentifier("response", Names.ResponseResultProperty),
                        SF.IdentifierName("result")));
            }

            yield return SF.ReturnStatement(SF.IdentifierName("response"));
        }

        private TypeSyntax GetValueTaskOf(ParamDeclaration param)
        {
            if (param == null || param.ParamType == null)
                return SF.ParseTypeName(_contract.Compatibility.GetAsyncWrapper());
            else
                return SH.GenericType(_contract.Compatibility.GetAsyncWrapper(), param.ParamType);
        }

        private TypeSyntax GetTypeSyntax(ParamDeclaration param)
        {
            if (param == null || param.ParamType == null)
                return SF.PredefinedType(SF.Token(SyntaxKind.VoidKeyword));
            else
                return SF.ParseTypeName(param.ParamType);
        }

        private MemberDeclarationSyntax GenerateHandlerOnInitOverride()
        {
            var stubInitInvoke = SF.InvocationExpression(SH.MemeberOfIdentifier("_stub", "InitServiceStub"))
                .AddArgumentListArguments(SH.IdentifierArgument("Session"));

            if (!_isCallbackStub && _contract.HasCallbacks)
            {
                var callbackClientCreation = SF.ObjectCreationExpression(SH.ShortTypeName(_contract.CallbackClientStubClassName))
                    .AddArgumentListArguments(SH.IdentifierArgument("channel"));

                stubInitInvoke = stubInitInvoke.AddArgumentListArguments(SF.Argument(callbackClientCreation));
            }

            return SF.MethodDeclaration(SH.VoidToken(), Names.RpcServiceBaseOnInitMethod)
               .AddModifiers(SF.Token(SyntaxKind.ProtectedKeyword), SF.Token(SyntaxKind.OverrideKeyword))
               .AddParameterListParameters(SH.Parameter("channel", Names.RpcChannelClass.Full))
               .AddBodyStatements(SF.ExpressionStatement(stubInitInvoke));
        }

        private MemberDeclarationSyntax GenerateClientStubProperty()
        {
            var clientStubType = SH.ShortTypeName(_contract.CallbackClientStubClassName);
            return SF.PropertyDeclaration(clientStubType, "Client")
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddPrivateAutoSetter();
        }

        private MemberDeclarationSyntax GenerateSessionProperty()
        {
            return SF.PropertyDeclaration(SF.IdentifierName(Names.RpcSessionInfoClass.Full), "Session")
                .AddModifiers(SH.PublicToken())
                .AddAutoGetter()
                .AddPrivateAutoSetter();
        }

        private MemberDeclarationSyntax GenerateOnInitMethod()
        {
            return SF.MethodDeclaration(SH.VoidToken(), "OnInit")
                .AddModifiers(SH.ProtectedToken(), SH.VirtualToken())
                .AddBodyStatements();
        }

        private MemberDeclarationSyntax GenerateStubInitServiceStubMethod()
        {
            var sessionParam = SH.Parameter("session", Names.RpcSessionInfoClass.Full);
            var sessionInitStatement = SH.AssignmentStatement(SF.IdentifierName("Session"), SF.IdentifierName("session"));

            var onInitInvoke = SF.ExpressionStatement(SH.InvocationExpression("OnInit"));

            var method = SF.MethodDeclaration(SH.VoidToken(), "InitServiceStub")
                .AddModifiers(SH.PublicToken())
                .AddParameterListParameters(sessionParam)
                .AddBodyStatements(sessionInitStatement, onInitInvoke);

            if (_contract.HasCallbacks)
            {
                var callbackClientParam = SH.Parameter("client", _contract.CallbackClientStubClassName.Short);
                var callbackClientInitStatement = SH.AssignmentStatement(SF.IdentifierName("Client"), SF.IdentifierName("client"));

                method = method.AddParameterListParameters(callbackClientParam)
                    .AddBodyStatements(callbackClientInitStatement);
            }

            return method;
        }

        private List<CallDeclaration> GetAffectedCalls()
        {
            return (_isCallbackStub ? _contract.Calls.Where(c => c.IsCallback)
                : _contract.Calls.Where(c => !c.IsCallback)).ToList();
        }

        private IEnumerable<CatchClauseSyntax> GenerateCustomCatches(CallDeclaration call)
        {
            foreach (var customFault in call.Faults)
                yield return GenerateCustomFaultCatch(customFault);
        }

        private CatchClauseSyntax GenerateRegularCatch()
        {
            var callIdArgument = SF.Argument(SH.MemeberOfIdentifier("request", "CallId"));
            var textArgument = SF.Argument(SH.MemeberOfIdentifier("ex", "Message"));

            var retStatement = SF.ReturnStatement(
                    SH.InvocationExpression(Names.ServiceOnRegularFaultMethod, callIdArgument, textArgument));

            return SF.CatchClause(SF.CatchDeclaration(SH.FullTypeName(Names.RpcFaultException), SF.Identifier("ex")),
                null, SF.Block(retStatement));
        }

        private CatchClauseSyntax GenerateUnexpectedCatch()
        {
            var callIdArgument = SF.Argument(SH.MemeberOfIdentifier("request", "CallId"));

            var retStatement = SF.ReturnStatement(
                    SH.InvocationExpression(Names.ServiceOnUnexpectedFaultMethod, callIdArgument, SH.IdentifierArgument("ex")));

            return SF.CatchClause(SF.CatchDeclaration(SF.ParseTypeName(Names.SystemException), SF.Identifier("ex")),
                null, SF.Block(retStatement));
        }

        private CatchClauseSyntax GenerateCustomFaultCatch(string faultType)
        {
            var callIdArgument = SF.Argument(SH.MemeberOfIdentifier("request", "CallId"));
            var faultArgument = SF.Argument(SH.MemeberOfIdentifier("ex", "Fault"));

            var retStatement = SF.ReturnStatement(
                    SH.InvocationExpression(Names.ServiceOnCustomFaultMethod, callIdArgument, faultArgument));

            var exceptionType = SH.GenericName(Names.RpcFaultException.Full, faultType);

            return SF.CatchClause(SF.CatchDeclaration(exceptionType, SF.Identifier("ex")),
                null, SF.Block(retStatement));
        }
    }
}
