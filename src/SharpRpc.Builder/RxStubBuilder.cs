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
                .AddMembers(GenerateStubMethods());

            if (_contract.EnablePostResponseMethods)
                stubClass = stubClass.AddMembers(GeneratePostResponseMethods());

            if (!_isCallbackStub)
            {
                stubClass = stubClass.AddMembers(GenerateSessionProperty());

                if (_contract.HasCallbacks)
                    stubClass = stubClass.AddMembers(GenerateClientStubProperty());

                stubClass = stubClass.AddMembers(GenerateOnInitMethod(), GenerateOnCloseMethod(), GenerateStubInitServiceStubMethod());
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

            var handlerBaseType = _isCallbackStub ? Names.RpcCallHandlerClass.Full : Names.RpcServiceCallHandlerClass.Full;

            var handlerClass = SF.ClassDeclaration(handlerClassName.Short)
                .AddModifiers(SF.Token(SyntaxKind.PrivateKeyword))
                .AddBaseListTypes(SF.SimpleBaseType(SF.ParseTypeName(handlerBaseType)))
                .AddMembers(serviceImplField, constructor)
                .AddMembers(GenerateWrapMethods())
                .AddMembers(GenerateOnMessageOverride(), GenerateOnRequestOverride());

            if (_contract.EnablePostResponseMethods)
                handlerClass = handlerClass.AddMembers(GenerateOnReponseSentOverride());

            if (!_isCallbackStub)
                handlerClass = handlerClass.AddMembers(GenerateHandlerOnInitOverride(), GenerateHandlerOnCloseMethod());

            return handlerClass;
        }

        public MethodDeclarationSyntax GenerateServiceDescriptorFactoryPrivate()
        {
            //var msgFactoryVarStatement = SH.LocalVarDeclaration("sFactory",
            //    SF.ObjectCreationExpression(SH.ShortTypeName(_contract.MessageFactoryClassName))
            //    .WithoutArguments());

            var descriptorVarStatement = SH.LocalVarDeclaration("descriptor",
                SH.InvocationExpression(Names.FacadeCreateDescriptorMethod, SH.IdentifierArgument("serializer")));

            var serviceFactoryFunc = SH.GenericType("System.Func", _contract.ServiceStubClassName.Short);
            var serviceFactoryParam = SH.Parameter("serviceImplFactory", serviceFactoryFunc);

            var handlerCreationExp = SF.ObjectCreationExpression(SH.ShortTypeName(_contract.ServiceHandlerClassName))
                .AddArgumentListArguments(SF.Argument(SF.InvocationExpression(SF.IdentifierName("serviceImplFactory"))));

            var handlerCreationLambda = SF.ParenthesizedLambdaExpression()
                .WithExpressionBody(handlerCreationExp);

            var retStatement = SF.ReturnStatement(
                SF.ObjectCreationExpression(SH.FullTypeName(Names.ServiceDescriptorClass))
                .AddArgumentListArguments(
                    SH.IdentifierArgument("descriptor"),
                    SF.Argument(handlerCreationLambda)));

            var serializerParam = SH.Parameter("serializer", Names.RpcSerializerInterface.Full);

            return SF.MethodDeclaration(SH.FullTypeName(Names.ServiceDescriptorClass), Names.FacadeCreateServiceMethod)
                .AddModifiers(SF.Token(SyntaxKind.PrivateKeyword), SF.Token(SyntaxKind.StaticKeyword))
                .AddParameterListParameters(serviceFactoryParam, serializerParam)
                .WithBody(SF.Block(descriptorVarStatement, retStatement));
        }

        public MethodDeclarationSyntax GenerateServiceDescriptorFactoryPublic(ParameterSyntax serializerParam, StatementSyntax serializerAdapterVarStatement, ArgumentSyntax serializerAdapterArg)
        {
            //var serializerAdapterCreateClause = SH.InvocationExpression(Names.FacadeSerializerAdapterFactoryMethod,
            //    SF.Argument(SF.IdentifierName("serializer")));
            //var serializerAdapterVarStatement = SH.LocalVarDeclaration("serializerAdapter", serializerAdapterCreateClause);

            var serviceFactoryFunc = SH.GenericType("System.Func", _contract.ServiceStubClassName.Short);
            var serviceFactoryParam = SH.Parameter("serviceImplFactory", serviceFactoryFunc);

            var retStatement = SF.ReturnStatement(
                SH.InvocationExpression(Names.FacadeCreateServiceMethod,
                    SH.IdentifierArgument("serviceImplFactory"), serializerAdapterArg));

            //var serializerDefValue = SH.EnumValue(Names.SerializerChoiceEnum.Full, _contract.GetDefaultSerializerChoice());
            //var serializerParam = SH.Parameter("serializer", Names.SerializerChoiceEnum.Full)
            //    .WithDefault(SF.EqualsValueClause(serializerDefValue));

            return SF.MethodDeclaration(SH.FullTypeName(Names.ServiceDescriptorClass), Names.FacadeCreateServiceMethod)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword), SF.Token(SyntaxKind.StaticKeyword))
                .AddParameterListParameters(serviceFactoryParam, serializerParam)
                .WithBody(SF.Block(serializerAdapterVarStatement, retStatement));
        }

        public MethodDeclarationSyntax GenerateServiceDescriptorFactoryPublic()
        {
            var serializerDefValue = SH.EnumValue(Names.SerializerChoiceEnum.Full, _contract.GetDefaultSerializerChoice());
            var serializerParam = SH.Parameter("serializer", Names.SerializerChoiceEnum.Full)
                .WithDefault(SF.EqualsValueClause(serializerDefValue));

            var serializerAdapterCreateClause = SH.InvocationExpression(Names.FacadeSerializerAdapterFactoryMethod,
                SH.IdentifierArgument("serializer"));
            var serializerAdapterVarStatement = SH.LocalVarDeclaration("serializerAdapter", serializerAdapterCreateClause);

            return GenerateServiceDescriptorFactoryPublic(serializerParam, serializerAdapterVarStatement, SH.IdentifierArgument("serializerAdapter"));
        }

        private MethodDeclarationSyntax[] GenerateStubMethods()
        {
            var methods = new List<MethodDeclarationSyntax>();

            foreach (var callDec in GetAffectedCalls())
                methods.Add(GenerateStubMethod(callDec));

            return methods.ToArray();
        }

        private MethodDeclarationSyntax[] GeneratePostResponseMethods()
        {
            var methods = new List<MethodDeclarationSyntax>();

            foreach (var callDec in GetAffectedCalls())
            {
                if (callDec.IsRequestResponceCall && callDec.ReturnsData)
                    methods.Add(GeneratePostResponseMethod(callDec));
            }

            return methods.ToArray();
        }

        private MethodDeclarationSyntax[] GenerateWrapMethods()
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

        private MethodDeclarationSyntax GenerateStubMethod(OperationDeclaration callDec)
        {
            var methodParams = new List<ParameterSyntax>();

            if (callDec.IsRequestResponceCall)
            {
                methodParams.Add(SF.Parameter(SF.Identifier(FindSafeParamName("context", callDec)))
                    .WithType(SH.FullTypeName(Names.RpcCallContextClass)));
            }

            if (callDec.HasInStream)
            {
                var paramSyntax = SF.Parameter(SF.Identifier(FindSafeParamName("inputStream", callDec)))
                    .WithType(Names.GetReaderStreamStubType(callDec.InStreamItemType));

                methodParams.Add(paramSyntax);
            }

            if (callDec.HasOutStream)
            {
                var paramSyntax = SF.Parameter(SF.Identifier(FindSafeParamName("outputStream", callDec)))
                    .WithType(Names.GetWriterStreamStubType(callDec.OutStreamItemType));

                methodParams.Add(paramSyntax);
            }

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

        private MethodDeclarationSyntax GeneratePostResponseMethod(OperationDeclaration callDec)
        {
            var methodName = "OnResponseSent_" + callDec.MethodName;
            var retType = GetTypeSyntax(callDec.ReturnParam);
            var param = SH.Parameter("responseValue", retType);

            return SF.MethodDeclaration(SH.VoidToken(), methodName)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword), SF.Token(SyntaxKind.VirtualKeyword))
                .AddParameterListParameters(param)
                .AddBodyStatements();
        }

        private MethodDeclarationSyntax GenerateRequestWrapMethod(OperationDeclaration callDec)
        {
            var methodName = "Invoke" + callDec.MethodName;
            var hasStreams = callDec.HasStreams;
            var requetsMessageType = _contract.GetRequestClassName(callDec);

            var handlerInvocationExp = SF.AwaitExpression(GenerateRpcInvocation(callDec, "request"));

            var tryBody = new List<StatementSyntax>();

            if (callDec.ReturnsData)
                tryBody.Add(SH.LocalVarDeclaration("result", handlerInvocationExp));
            else
                tryBody.Add(SF.ExpressionStatement(handlerInvocationExp));

            tryBody.Add(GenerateContextCloseStatement(hasStreams));
            tryBody.AddRange(GenerateResponseCreationStatements(callDec));

            var statements = new List<StatementSyntax>();

            statements.Add(SH.LocalVarDeclaration("context", GenerateContextCreationExp(callDec)));

            statements.Add(SF.TryStatement()
                .AddCatches(GenerateCustomCatches(callDec, hasStreams).ToArray())
                .AddCatches(GenerateRegularCatch(callDec, hasStreams), GenerateUnexpectedCatch(callDec, hasStreams))
                .AddBlockStatements(tryBody.ToArray()));

            var retType = SH.GenericType(_contract.Compatibility.GetAsyncWrapper(), Names.ResponseInterface.Full);

            return SF.MethodDeclaration(retType, methodName)
               .AddModifiers(SF.Token(SyntaxKind.PrivateKeyword), SF.Token(SyntaxKind.AsyncKeyword))
               .AddParameterListParameters(SH.Parameter("request", requetsMessageType.Full))
               .WithBody(SF.Block(statements));
        }

        private InvocationExpressionSyntax GenerateContextCreationExp(OperationDeclaration callDec)
        {
            if (callDec.HasStreams)
                return GenerateStreamContextCreationExp(callDec);
            else
                return SF.InvocationExpression(SF.IdentifierName("CreateCallContext"))
                        .AddArgumentListArguments(SH.IdentifierArgument("request"));
        }

        private InvocationExpressionSyntax GenerateStreamContextCreationExp(OperationDeclaration callDec)
        {
            if (callDec.HasInStream)
            {
                if (callDec.HasOutStream)
                {
                    var streamHandlerMethod = SH.GenericName("CreateDuplexStreamContext", callDec.InStreamItemType, callDec.OutStreamItemType);
                    var inFactory = TxStubBuilder.GenerateStreamFactoryCreationExp(_contract.GetInputStreamFactoryClassName(callDec));
                    var outFactory = TxStubBuilder.GenerateStreamFactoryCreationExp(_contract.GetOutputStreamFactoryClassName(callDec));
                    return SF.InvocationExpression(streamHandlerMethod)
                        .AddArgumentListArguments(SH.IdentifierArgument("request"), SF.Argument(inFactory), SF.Argument(outFactory));
                }
                else
                {
                    var streamHandlerMethod = SH.GenericName("CreateInputStreamContext", callDec.InStreamItemType);
                    var factory = TxStubBuilder.GenerateStreamFactoryCreationExp(_contract.GetInputStreamFactoryClassName(callDec));
                    return SF.InvocationExpression(streamHandlerMethod)
                        .AddArgumentListArguments(SH.IdentifierArgument("request"), SF.Argument(factory));
                }
            }
            else
            {
                var streamHandlerMethod = SH.GenericName("CreateOutputStreamContext", callDec.OutStreamItemType);
                var factory = TxStubBuilder.GenerateStreamFactoryCreationExp(_contract.GetOutputStreamFactoryClassName(callDec));
                return SF.InvocationExpression(streamHandlerMethod)
                    .AddArgumentListArguments(SH.IdentifierArgument("request"), SF.Argument(factory));
            }
        }

        private StatementSyntax GenerateContextCloseStatement(bool hasStreams)
        {
            var methodToInvoke = hasStreams ? "CloseStreamContext" : "CloseContext";

            var closeInvocation = SF.InvocationExpression(SF.IdentifierName(methodToInvoke))
                    .AddArgumentListArguments(SH.IdentifierArgument("context"));

            if (hasStreams)
                return SF.ExpressionStatement(SF.AwaitExpression(closeInvocation));
            else
                return SF.ExpressionStatement(closeInvocation);
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
                    var messageType = _contract.GetOnWayMessageClassName(call);
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

                        var castVariable = SH.LocalVarDeclaration(typedMessageVarName,
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
                    var messageType = _contract.GetRequestClassName(call);
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

                        var castVariable = SH.LocalVarDeclaration(typedMessageVarName,
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

        private MethodDeclarationSyntax GenerateOnReponseSentOverride()
        {
            StatementSyntax ifRoot = null;

            var index = 0;

            foreach (var call in GetAffectedCalls())
            {
                if (call.IsRequestResponceCall && call.ReturnsData)
                {
                    var messageType = _contract.GetResponseClassName(call);
                    var typedMessageVarName = "r" + index++;
                    var methodToInvoke = "OnResponseSent_" + call.MethodName;

                    var respValueArg = SF.Argument(SH.MemberOfIdentifier(typedMessageVarName, "Result"));

                    var rpcMethodCall = SF.ExpressionStatement(
                        SF.InvocationExpression(SH.MemberOf(SF.IdentifierName("_stub"), methodToInvoke),
                        SH.CallArguments(respValueArg)));

                    ExpressionSyntax ifExpression;
                    StatementSyntax ifBody;

                    if (_contract.Compatibility.SupportsPatternMatching)
                    {
                        ifExpression = SF.IsPatternExpression(SF.IdentifierName("response"),
                            SF.DeclarationPattern(SF.ParseTypeName(messageType.Full), SF.SingleVariableDesignation(SF.Identifier(typedMessageVarName))));

                        ifBody = rpcMethodCall;
                    }
                    else
                    {
                        ifExpression = SF.BinaryExpression(SyntaxKind.IsExpression,
                               SF.IdentifierName("response"), SF.ParseTypeName(messageType.Full));

                        var castVariable = SH.LocalVarDeclaration(typedMessageVarName,
                            SF.CastExpression(SF.ParseTypeName(messageType.Full), SF.IdentifierName("response")));

                        ifBody = SF.Block(castVariable, rpcMethodCall);
                    }

                    if (ifRoot != null)
                        ifRoot = SF.IfStatement(ifExpression, ifBody, SF.ElseClause(ifRoot));
                    else
                        ifRoot = SF.IfStatement(ifExpression, ifBody);
                }
            }

            var body = SF.Block();

            if (ifRoot != null)
                body = body.AddStatements(ifRoot);

            return SF.MethodDeclaration(SH.VoidToken(), Names.RpcServiceBaseOnResponseSentMethod)
                .AddModifiers(SF.Token(SyntaxKind.ProtectedKeyword), SF.Token(SyntaxKind.OverrideKeyword))
                .AddParameterListParameters(SH.Parameter("response", Names.ResponseInterface.Full))
                .WithBody(body);
        }

        private ExpressionSyntax GenerateRpcInvocation(OperationDeclaration callDec, string typedMessageVarName)
        {
            var args = new List<ArgumentSyntax>();

            if (callDec.IsRequestResponceCall)
                args.Add(SH.IdentifierArgument("context"));

            if (callDec.HasInStream)
                args.Add(SF.Argument(SH.MemberOfIdentifier("context", "InputStream")));

            if (callDec.HasOutStream)
                args.Add(SF.Argument(SH.MemberOfIdentifier("context", "OutputStream")));

            foreach (var param in callDec.Params)
                args.Add(SF.Argument(SH.MemberOfIdentifier(typedMessageVarName, param.MessagePropertyName)));

            var methodName = callDec.MethodName;

            return SF.InvocationExpression(SH.MemberOf(SF.IdentifierName("_stub"), methodName), SH.CallArguments(args));
        }

        private IEnumerable<StatementSyntax> GenerateResponseCreationStatements(OperationDeclaration callDec)
        {
            var responseClassName = _contract.GetResponseClassName(callDec);
            var respomseCreationExp = SF.ObjectCreationExpression(SF.ParseTypeName(responseClassName.Full)).WithoutArguments();

            yield return SH.LocalVarDeclaration("response", respomseCreationExp);

            if (callDec.ReturnsData)
            {
                yield return SF.ExpressionStatement(
                    SF.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                        SH.MemberOfIdentifier("response", Names.ResponseResultProperty),
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
            var stubInitInvoke = SF.InvocationExpression(SH.MemberOfIdentifier("_stub", "InitServiceStub"))
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

        private MemberDeclarationSyntax GenerateHandlerOnCloseMethod()
        {
            var stubInitInvoke = SF.InvocationExpression(SH.MemberOfIdentifier("_stub", "OnClose"))
                .AddArgumentListArguments();

            return SF.MethodDeclaration(SH.VoidToken(), Names.RpcServiceBaseOnCloseMethod)
               .AddModifiers(SF.Token(SyntaxKind.ProtectedKeyword), SF.Token(SyntaxKind.OverrideKeyword))
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
                .AddModifiers(SH.PublicToken(), SH.VirtualToken())
                .AddBodyStatements();
        }

        private MemberDeclarationSyntax GenerateOnCloseMethod()
        {
            return SF.MethodDeclaration(SH.VoidToken(), "OnClose")
                .AddModifiers(SH.PublicToken(), SH.VirtualToken())
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
                .AddBodyStatements(sessionInitStatement);

            if (_contract.HasCallbacks)
            {
                var callbackClientParam = SH.Parameter("client", _contract.CallbackClientStubClassName.Short);
                var callbackClientInitStatement = SH.AssignmentStatement(SF.IdentifierName("Client"), SF.IdentifierName("client"));

                method = method.AddParameterListParameters(callbackClientParam)
                    .AddBodyStatements(callbackClientInitStatement);
            }

            return method
                .AddBodyStatements(onInitInvoke);
        }

        private List<OperationDeclaration> GetAffectedCalls()
        {
            return (_isCallbackStub ? _contract.Operations.Where(c => c.IsCallback)
                : _contract.Operations.Where(c => !c.IsCallback)).ToList();
        }

        private IEnumerable<CatchClauseSyntax> GenerateCustomCatches(OperationDeclaration call, bool hasStream)
        {
            foreach (var record in call.CustomFaults)
                yield return GenerateCustomFaultCatch(record.Item1, record.Item2, call, hasStream);
        }

        private CatchClauseSyntax GenerateRegularCatch(OperationDeclaration opContract, bool hasStreams)
        {
            var textArgument = SF.Argument(SH.MemberOfIdentifier("ex", "Message"));
            var msgArgument = SH.IdentifierArgument("faultMsg");

            var catchBody = new List<StatementSyntax>();
            catchBody.Add(GenerateContextCloseStatement(hasStreams));
            catchBody.Add(GenerateFaultMessageCrationStatement(opContract));
            catchBody.Add(SF.ReturnStatement(
                    SH.InvocationExpression(Names.ServiceOnRegularFaultMethod, msgArgument, textArgument)));

            return SF.CatchClause(SF.CatchDeclaration(SH.FullTypeName(Names.RpcFaultException), SF.Identifier("ex")),
                null, SF.Block(catchBody));
        }

        private CatchClauseSyntax GenerateUnexpectedCatch(OperationDeclaration opContract, bool hasStreams)
        {
            var catchBody = new List<StatementSyntax>();
            catchBody.Add(GenerateContextCloseStatement(hasStreams));
            catchBody.Add(GenerateFaultMessageCrationStatement(opContract));
            catchBody.Add(SF.ReturnStatement(
                    SH.InvocationExpression(Names.ServiceOnUnexpectedFaultMethod, SH.IdentifierArgument("faultMsg"), SH.IdentifierArgument("ex"))));

            return SF.CatchClause(SF.CatchDeclaration(SF.ParseTypeName(Names.SystemException), SF.Identifier("ex")),
                null, SF.Block(catchBody));
        }

        private CatchClauseSyntax GenerateCustomFaultCatch(ushort faultKey, string faultType, OperationDeclaration opDec, bool hasStreams)
        {
            var messageArgument = SH.IdentifierArgument("faultMsg");
            var textArgument = SF.Argument(SH.MemberOfIdentifier("ex", "Message"));

            var catchBody = new List<StatementSyntax>();
            catchBody.Add(GenerateContextCloseStatement(hasStreams));
            catchBody.Add(GenerateFaultMessageCrationStatement(opDec));

            var adapterClassName = _contract.GetFaultAdapterClassName(faultKey, opDec);
            var adapterCreationExp = SF.ObjectCreationExpression(SH.FullTypeName(adapterClassName))
                .AddInitializer(SH.AssignmentExpression(SF.IdentifierName("Data"), SH.MemberOfIdentifier("ex", "Fault")));

            catchBody.Add(SH.AssignmentStatement(
                SH.MemberOfIdentifier("faultMsg", "CustomFaultBinding"),
                adapterCreationExp));

            catchBody.Add(SF.ReturnStatement(
                    SH.InvocationExpression(Names.ServiceOnCustomFaultMethod, messageArgument, textArgument)));

            var exceptionType = SH.GenericName(Names.RpcFaultException.Full, faultType);

            return SF.CatchClause(SF.CatchDeclaration(exceptionType, SF.Identifier("ex")),
                null, SF.Block(catchBody));
        }

        private StatementSyntax GenerateFaultMessageCrationStatement(OperationDeclaration opContract)
        {
            var faultMsgType = _contract.GetFaultMessageClassName(opContract);

            var faultMsgCrationExp = SF.ObjectCreationExpression(SH.FullTypeName(faultMsgType))
                .AddArgumentListArguments();

            return SH.LocalVarDeclaration("faultMsg", faultMsgCrationExp);
        }

        private string FindSafeParamName(string initialParamName, OperationDeclaration callDec)
        {
            if (callDec.HasParameterWithName(initialParamName))
            {
                for (int i = 0; i < int.MaxValue; i++)
                {
                    var newName = initialParamName + i;
                    if (!callDec.HasParameterWithName(newName))
                        return newName;
                }
            }

            return initialParamName;
        }
    }
}
