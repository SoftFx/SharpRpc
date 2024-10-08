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
using System.Collections.Generic;
using System.Text;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using SH = SharpRpc.Builder.SyntaxHelper;

namespace SharpRpc.Builder
{
    internal class TxStubBuilder
    {
        private readonly ContractDeclaration _contract;
        private readonly bool _isCallback;

        public TxStubBuilder(ContractDeclaration contract, bool isCallbackClient)
        {
            _contract = contract;
            _isCallback = isCallbackClient;
        }

        public ClassDeclarationSyntax GenerateCode(MetadataDiagnostics diagnostics)
        {
            if (_isCallback)
                return GenerateServerSideStub(diagnostics);
            else
                return GenerateClientSideStub(diagnostics);
        }

        private ClassDeclarationSyntax GenerateClientSideStub(MetadataDiagnostics diagnostics)
        {
            var clientStubType = _contract.ClientStubClassName;
            var addHandlerParam = !_isCallback && _contract.HasCallbacks;

            var constructorInitializer = SF.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
                .AddArgumentListArguments(SH.IdentifierArgument("endpoint"), SH.IdentifierArgument("descriptor"));

            if (addHandlerParam)
            {
                var handlerCreationExp = SF.ObjectCreationExpression(SH.ShortTypeName(_contract.CallbackHandlerClassName))
                    .AddArgumentListArguments(SH.IdentifierArgument("callbackHandler"));
                constructorInitializer = constructorInitializer.AddArgumentListArguments(SF.Argument(handlerCreationExp));
            }

            var endpointConsParam = SH.Parameter("endpoint", Names.RpcClientEndpointBaseClass.Full);
            var descriptorConsParam = SH.Parameter("descriptor", Names.ContractDescriptorClass.Full);

            var constructorBody = SF.Block(
                GenerateFacadePropertyInitializer(true, false),
                GenerateFacadePropertyInitializer(false, true),
                GenerateFacadePropertyInitializer(true, true));

            var constructor = SF.ConstructorDeclaration(clientStubType.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(endpointConsParam, descriptorConsParam)
                .WithInitializer(constructorInitializer)
                .WithBody(constructorBody);

            var asyncFacade = GenerateDedicatedFacade(true, false, diagnostics);
            var tryFacade = GenerateDedicatedFacade(false, true, diagnostics);
            var asyncTryFacade = GenerateDedicatedFacade(true, true, diagnostics);

            var asyncFacadeProperty = GenerateFacadeProperty(true, false);
            var tryFacadeProperty = GenerateFacadeProperty(false, true);
            var asyncTryFacadeProperty = GenerateFacadeProperty(true, true);

            if (addHandlerParam)
            {
                var handlerType = SH.ShortTypeName(_contract.CallbackServiceStubClassName);
                var handlerConsParam = SH.Parameter("callbackHandler", handlerType);
                constructor = constructor.AddParameterListParameters(handlerConsParam);
            }

            return SF.ClassDeclaration(clientStubType.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SF.SimpleBaseType(SF.ParseTypeName(Names.RpcClientBaseClass.Full)))
                .AddMembers(constructor, asyncFacadeProperty, tryFacadeProperty, asyncTryFacadeProperty)
                .AddMembers(GenerateCallMethods(clientStubType, false, false, false, diagnostics))
                .AddMembers(asyncFacade, tryFacade, asyncTryFacade);
        }

        private PropertyDeclarationSyntax GenerateFacadeProperty(bool isAsync, bool isTry)
        {
            var propertyName = GetFacadePropertyName(isAsync, isTry);
            var propertyType = GetFacadeClassName(isAsync, isTry);

            return SF.PropertyDeclaration(SF.IdentifierName(propertyType), propertyName)
                .AddModifiers(SH.PublicToken())
                .AddAutoGetter();
        }

        private StatementSyntax GenerateFacadePropertyInitializer(bool isAsync, bool isTry)
        {
            var facadeType = GetFacadeClassName(isAsync, isTry);
            var facadeCreationExp = SF.ObjectCreationExpression(SF.IdentifierName(facadeType))
                .AddArgumentListArguments(SH.IdentifierArgument("Channel"));

            return SH.AssignmentStatement(
                SF.IdentifierName(GetFacadePropertyName(isAsync, isTry)),
                facadeCreationExp);
        }

        private ClassDeclarationSyntax GenerateDedicatedFacade(bool isAsync, bool isTry, MetadataDiagnostics diagnostics)
        {
            var className = GetFacadeClassName(isAsync, isTry);

            var channelParam = SH.Parameter("channel", Names.RpcChannelClass.Full);

            var constructorInitializer = SF.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
                .AddArgumentListArguments(SH.IdentifierArgument("channel"));

            var constructor = SF.ConstructorDeclaration(className)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(channelParam)
                .WithInitializer(constructorInitializer)
                .WithBody(SF.Block());

            return SF.ClassDeclaration(className)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddMembers(constructor)
                .AddBaseListTypes(SF.SimpleBaseType(SF.ParseTypeName(Names.RpcClientFacadeBaseClass.Full)))
                .AddMembers(GenerateCallMethods(_contract.ClientStubClassName, isAsync, isTry, true, diagnostics));
        }

        private ClassDeclarationSyntax GenerateServerSideStub(MetadataDiagnostics diagnostics)
        {
            var clientStubType = _contract.CallbackClientStubClassName;

            var constructorInitializer = SF.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
                .AddArgumentListArguments(SH.IdentifierArgument("channel"));

            var channelConsParam = SH.Parameter("channel", Names.RpcChannelClass.Full);

            var constructorBody = SF.Block(
                GenerateFacadePropertyInitializer(true, false),
                GenerateFacadePropertyInitializer(false, true),
                GenerateFacadePropertyInitializer(true, true));

            var constructor = SF.ConstructorDeclaration(clientStubType.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(channelConsParam)
                .WithInitializer(constructorInitializer)
                .WithBody(constructorBody);

            var asyncFacade = GenerateDedicatedFacade(true, false, diagnostics);
            var tryFacade = GenerateDedicatedFacade(false, true, diagnostics);
            var asyncTryFacade = GenerateDedicatedFacade(true, true, diagnostics);

            var asyncFacadeProperty = GenerateFacadeProperty(true, false);
            var tryFacadeProperty = GenerateFacadeProperty(false, true);
            var asyncTryFacadeProperty = GenerateFacadeProperty(true, true);


            return SF.ClassDeclaration(clientStubType.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SF.SimpleBaseType(SF.ParseTypeName(Names.RpcClientBaseClass.Full)))
                .AddMembers(constructor, asyncFacadeProperty, tryFacadeProperty, asyncTryFacadeProperty)
                .AddMembers(GenerateCallMethods(clientStubType, false, false, false, diagnostics))
                .AddMembers(asyncFacade, tryFacade, asyncTryFacade);
        }

        public MethodDeclarationSyntax GenerateFactoryMethodPrivate()
        {
            var addHandlerParam = !_isCallback && _contract.HasCallbacks;

            var descriptorVarStatement = SH.LocalVarDeclaration("descriptor",
                SH.InvocationExpression(Names.FacadeCreateDescriptorMethod, SH.IdentifierArgument("serializer")));

            var clientCreateExpression = SF.ObjectCreationExpression(SH.ShortTypeName(_contract.ClientStubClassName))
                .WithArgumentList(SH.CallArguments(SH.IdentifierArgument("endpoint"), SH.IdentifierArgument("descriptor")));

            if (addHandlerParam)
                clientCreateExpression = clientCreateExpression.AddArgumentListArguments(SH.IdentifierArgument("callbackHandler"));

            var returnStatement = SF.ReturnStatement(clientCreateExpression);

            var paramList = new List<ParameterSyntax>
            {
                SH.Parameter("endpoint", Names.RpcClientEndpointBaseClass.Full),
                SH.Parameter("serializer", Names.RpcSerializerInterface.Full),
            };

            if (addHandlerParam)
            {
                var handlerType = SH.ShortTypeName(_contract.CallbackServiceStubClassName);
                paramList.Add(SH.Parameter("callbackHandler", handlerType));
            }

            return SF.MethodDeclaration(SF.ParseTypeName(_contract.ClientStubClassName.Short), Names.FacadeCreateClientMethod)
                .AddModifiers(SF.Token(SyntaxKind.PrivateKeyword), SF.Token(SyntaxKind.StaticKeyword))
                .AddParameterListParameters(paramList.ToArray())
                .WithBody(SF.Block(descriptorVarStatement, returnStatement));
        }

        public MethodDeclarationSyntax GenerateFactoryMethodPublic(ParameterSyntax serializerParam, StatementSyntax serializerAdapterVarStatement, ArgumentSyntax serializerAdapterArg)
        {
            var addHandlerParam = !_isCallback && _contract.HasCallbacks;

            //var serializerAdapterCreateClause = SH.InvocationExpression(Names.FacadeSerializerAdapterFactoryMethod,
            //    SF.Argument(SF.IdentifierName("serializer")));
            //var serializerAdapterVarStatement = SH.LocalVarDeclaration("serializerAdapter", serializerAdapterCreateClause);

            var createClientArguments = new List<ArgumentSyntax>
            {
                SH.IdentifierArgument("endpoint"),
                serializerAdapterArg,
                //SH.IdentifierArgument("serializerAdapter"),
            };
            if (addHandlerParam)
                createClientArguments.Add(SH.IdentifierArgument("callbackHandler"));

            var retStatement = SF.ReturnStatement(
                SH.InvocationExpression(Names.FacadeCreateClientMethod, createClientArguments.ToArray()));

            var endpointParam = SH.Parameter("endpoint", Names.RpcClientEndpointBaseClass.Full);

            //var serializerDefValue = SH.EnumValue(Names.SerializerChoiceEnum.Full, _contract.GetDefaultSerializerChoice());
            //var serializerParam = SH.Parameter("serializer", Names.SerializerChoiceEnum.Full)
            //    .WithDefault(SF.EqualsValueClause(serializerDefValue));

            var paramList = new List<ParameterSyntax> { endpointParam };

            if (addHandlerParam)
            {
                var handlerType = SH.ShortTypeName(_contract.CallbackServiceStubClassName);
                paramList.Add(SH.Parameter("callbackHandler", handlerType));
            }

            paramList.Add(serializerParam);

            return SF.MethodDeclaration(SF.ParseTypeName(_contract.ClientStubClassName.Short), Names.FacadeCreateClientMethod)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword), SF.Token(SyntaxKind.StaticKeyword))
                .AddParameterListParameters(paramList.ToArray())
                .WithBody(SF.Block(serializerAdapterVarStatement, retStatement));
        }

        public MethodDeclarationSyntax GenerateFactoryMethodPublic()
        {
            var serializerDefValue = SH.EnumValue(Names.SerializerChoiceEnum.Full, _contract.GetDefaultSerializerChoice());
            var serializerParam = SH.Parameter("serializer", Names.SerializerChoiceEnum.Full)
                .WithDefault(SF.EqualsValueClause(serializerDefValue));

            var serializerAdapterCreateClause = SH.InvocationExpression(Names.FacadeSerializerAdapterFactoryMethod,
                SF.Argument(SF.IdentifierName("serializer")));
            var serializerAdapterVarStatement = SH.LocalVarDeclaration("serializerAdapter", serializerAdapterCreateClause);

            return GenerateFactoryMethodPublic(serializerParam, serializerAdapterVarStatement, SH.IdentifierArgument("serializerAdapter"));
        }

        private MethodDeclarationSyntax[] GenerateCallMethods(TypeString clientStubTypeName, bool isAsync, bool isTry, bool skipStreamCalls, MetadataDiagnostics diagnostics)
        {
            var methods = new List<MethodDeclarationSyntax>();

            foreach (var callDec in _contract.Operations)
            {
                if (callDec.HasStreams)
                {
                    if (!skipStreamCalls && (_isCallback && callDec.IsCallback || !_isCallback && !callDec.IsCallback))
                    {
                        if (callDec.IsOneWay)
                        {
                            // TO DO: emit warning
                        }

                        methods.Add(GenerateStreamCall(callDec));
                    }
                }
                else
                {
                    bool addMessage = _isCallback ? (callDec.CallType == ContractCallType.MessageToClient) : (callDec.CallType == ContractCallType.MessageToServer);
                    bool addCall = _isCallback ? (callDec.CallType == ContractCallType.CallToClient) : (callDec.CallType == ContractCallType.CallToServer);

                    if (addMessage)
                    {
                        methods.Add(GenerateOneWayCall(callDec, isAsync, isTry));

                        if (_contract.EnablePrebuild)
                            methods.Add(GeneratePrebuiltMessageSender(callDec, isAsync, isTry));
                    }
                    else if (addCall)
                        methods.Add(GenerateCall(callDec, isAsync, isTry));
                }
            }

            return methods.ToArray();
        }

        private MethodDeclarationSyntax GenerateOneWayCall(OperationDeclaration callDec, bool isAsync, bool isTry)
        {
            var bodyStatements = new List<StatementSyntax>();
            var methodParams = GenerateMethodParams(callDec);

            var msgClassName = _contract.GetOnWayMessageClassName(callDec);

            bodyStatements.AddRange(GenerateCreateAndFillMessageStatements(callDec, msgClassName));
            bodyStatements.Add(GenerateSendMessageStatement(isAsync, isTry, out var retType));

            var methodName = callDec.MethodName; // AtttributeMethodName(callDec, isAsync, isTry);

            var method = SF.MethodDeclaration(retType, methodName)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(methodParams.ToArray())
                .WithBody(SF.Block(bodyStatements));

            return method;
        }

        private MethodDeclarationSyntax GeneratePrebuiltMessageSender(OperationDeclaration callDec, bool isAsync, bool isTry)
        {
            var bodyStatements = new List<StatementSyntax>();
            var msgClassName = _contract.GetPrebuiltMessageClassName(callDec.MethodName);
            var msgParam = SH.Parameter("message", SH.FullTypeName(msgClassName));

            bodyStatements.Add(GenerateSendMessageStatement(isAsync, isTry, out var retType));

            var methodName = callDec.MethodName; // AtttributeMethodName(callDec, isAsync, isTry);

            var method = SF.MethodDeclaration(retType, methodName)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(msgParam)
                .WithBody(SF.Block(bodyStatements));

            return method;
        }

        private MethodDeclarationSyntax GenerateCall(OperationDeclaration callDec, bool isAsync, bool isTry)
        {
            var bodyStatements = new List<StatementSyntax>();
            var methodParams = GenerateMethodParams(callDec);

            methodParams.Add(SH.Parameter("cancelToken", "System.Threading.CancellationToken")
                .WithDefault(SF.EqualsValueClause(SF.DefaultExpression(SF.IdentifierName("System.Threading.CancellationToken")))));

            var requestMsgClassName = _contract.GetRequestClassName(callDec);
            var responseMsgClassName = _contract.GetResponseClassName(callDec);

            bodyStatements.AddRange(GenerateCreateAndFillMessageStatements(callDec, requestMsgClassName));

            TypeSyntax methodRetType;

            if (!callDec.ReturnsData)
                bodyStatements.Add(GenerateRemoteCallStatement(responseMsgClassName, isAsync, isTry, out methodRetType));
            else
                bodyStatements.Add(GenerateRemoteCallStatement(responseMsgClassName, callDec.ReturnParam.ParamType, isAsync, isTry, out methodRetType));

            var method = SF.MethodDeclaration(methodRetType, callDec.MethodName)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(methodParams.ToArray())
                .WithBody(SF.Block(bodyStatements));

            return method;
        }

        private MethodDeclarationSyntax GenerateStreamCall(OperationDeclaration callDec)
        {
            NameSyntax callProxyClass;
            NameSyntax methodToInvoke;

            if (callDec.HasInStream)
            {
                if (callDec.HasOutStream)
                {
                    if (callDec.ReturnsData)
                    {
                        callProxyClass = SH.GenericName(Names.RpcDuplexStreamCallClass.Full, callDec.InStreamItemType, callDec.OutStreamItemType, callDec.ReturnParam.ParamType);
                        methodToInvoke = SH.GenericName("OpenDuplexStream", callDec.InStreamItemType, callDec.OutStreamItemType, callDec.ReturnParam.ParamType);
                    }
                    else
                    {
                        callProxyClass = SH.GenericName(Names.RpcDuplexStreamCallClass.Full, callDec.InStreamItemType, callDec.OutStreamItemType);
                        methodToInvoke = SH.GenericName("OpenDuplexStream", callDec.InStreamItemType, callDec.OutStreamItemType);
                    }
                }
                else
                {
                    if (callDec.ReturnsData)
                    {
                        callProxyClass = SH.GenericName(Names.RpcInputStreamCallClass.Full, callDec.InStreamItemType, callDec.ReturnParam.ParamType);
                        methodToInvoke = SH.GenericName("OpenInputStream", callDec.InStreamItemType, callDec.ReturnParam.ParamType);
                    }
                    else
                    {
                        callProxyClass = SH.GenericName(Names.RpcInputStreamCallClass.Full, callDec.InStreamItemType);
                        methodToInvoke = SH.GenericName("OpenInputStream", callDec.InStreamItemType);
                    }
                }
            }
            else
            {
                if (callDec.ReturnsData)
                {
                    callProxyClass = SH.GenericName(Names.RpcOutputStreamCallClass.Full, callDec.OutStreamItemType, callDec.ReturnParam.ParamType);
                    methodToInvoke = SH.GenericName("OpenOutputStream", callDec.OutStreamItemType, callDec.ReturnParam.ParamType);
                }
                else
                {
                    callProxyClass = SH.GenericName(Names.RpcOutputStreamCallClass.Full, callDec.OutStreamItemType);
                    methodToInvoke = SH.GenericName("OpenOutputStream", callDec.OutStreamItemType);
                }
            }

            var bodyStatements = new List<StatementSyntax>();
            var methodParams = new List<ParameterSyntax>();

            if (callDec.HasInStream && callDec.HasOutStream)
                methodParams.Add(SH.Parameter("streamOptions", "SharpRpc.DuplexStreamOptions"));
            else
                methodParams.Add(SH.Parameter("streamOptions", "SharpRpc.StreamOptions"));

            methodParams.AddRange(GenerateMethodParams(callDec));

            //methodParams.Add(SH.Parameter("cancelToken", "System.Threading.CancellationToken")
            //    .WithDefault(SF.EqualsValueClause(SF.DefaultExpression(SF.IdentifierName("System.Threading.CancellationToken")))));

            var msgClassName = _contract.GetRequestClassName(callDec);

            var openStreamInvoke = SF.InvocationExpression(methodToInvoke)
                .AddArgumentListArguments(SH.IdentifierArgument("message"), SH.IdentifierArgument("streamOptions"));

            if (callDec.HasInStream)
            {
                openStreamInvoke = openStreamInvoke.AddArgumentListArguments(
                    SF.Argument(GenerateStreamFactoryCreationExp(_contract.GetInputStreamFactoryClassName(callDec))));
            }

            if (callDec.HasOutStream)
            {
                openStreamInvoke = openStreamInvoke.AddArgumentListArguments(
                    SF.Argument(GenerateStreamFactoryCreationExp(_contract.GetOutputStreamFactoryClassName(callDec))));
            }

            //openStreamInvoke = openStreamInvoke.AddArgumentListArguments(SH.IdentifierArgument("cancelToken"));

            bodyStatements.AddRange(GenerateCreateAndFillMessageStatements(callDec, msgClassName));
            bodyStatements.Add(SF.ReturnStatement(openStreamInvoke));

            var methodName = callDec.MethodName; // AtttributeMethodName(callDec, isAsync, isTry);

            var method = SF.MethodDeclaration(callProxyClass, methodName)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(methodParams.ToArray())
                .WithBody(SF.Block(bodyStatements));

            return method;
        }

        internal static List<ParameterSyntax> GenerateMethodParams(OperationDeclaration callDec)
        {
            var methodParams = new List<ParameterSyntax>();

            foreach (var param in callDec.Params)
            {
                var paramSyntax = SF
                    .Parameter(SF.Identifier(param.ParamName))
                    .WithType(GetTypeSyntax(param));

                methodParams.Add(paramSyntax);
            }

            return methodParams;
        }

        internal static IEnumerable<StatementSyntax> GenerateCreateAndFillMessageStatements(OperationDeclaration callDec, TypeString msgClassName)
        {
            var msgCreateClause = SF.EqualsValueClause(
                SF.ObjectCreationExpression(SF.ParseTypeName(msgClassName.Full))
                .WithArgumentList(SF.ArgumentList()));

            yield return SH.LocalVariableDeclaration(msgClassName.Full, "message", msgCreateClause);

            foreach (var paramDec in callDec.Params)
            {
                yield return SH.AssignmentStatement(
                    SH.MemberOfIdentifier("message", paramDec.MessagePropertyName),
                    SF.IdentifierName(paramDec.ParamName));
            }
        }

        private StatementSyntax GenerateSendMessageStatement(bool isAsync, bool isTry, out TypeSyntax retType)
        {
            var msgArgument = SF.Argument(SF.IdentifierName("message"));

            if (isAsync)
            {
                if (isTry)
                {
                    retType = SH.GenericType(_contract.Compatibility.GetAsyncWrapper(), Names.RpcResultStruct.Full);

                    return SF.ReturnStatement(
                        SF.InvocationExpression(SF.IdentifierName("TrySendMessageAsync"), SH.CallArguments(msgArgument)));
                }
                else
                {
                    retType = SF.ParseTypeName(_contract.Compatibility.GetAsyncWrapper());

                    return SF.ReturnStatement(
                        SF.InvocationExpression(SF.IdentifierName("SendMessageAsync"), SH.CallArguments(msgArgument)));
                }
            }
            else
            {
                if (isTry)
                {
                    retType = SF.ParseTypeName(Names.RpcResultStruct.Full);

                    return SF.ReturnStatement(
                        SF.InvocationExpression(SF.IdentifierName("TrySendMessage"), SH.CallArguments(msgArgument)));
                }
                else
                {
                    retType = SF.PredefinedType(SF.Token(SyntaxKind.VoidKeyword));

                    return SH.ThisCallStatement("SendMessage", SH.IdentifierArgument("message"));
                }
            }
        }

        private StatementSyntax GenerateRemoteCallStatement(TypeString respMessageType, bool isAsync, bool isTry, out TypeSyntax retType)
        {
            var msgArgument = SF.Argument(SF.IdentifierName("message"));
            var tokenArgument = SF.Argument(SF.IdentifierName("cancelToken"));

            var methodName = isTry ? "TryCallAsync" : "CallAsync";
            var methodToInvoke = SH.GenericName(methodName, respMessageType.Full);

            if (isAsync)
            {
                if (isTry)
                {
                    retType = SH.GenericType(Names.SystemTask, Names.RpcResultStruct.Full);

                    return SF.ReturnStatement(
                        SF.InvocationExpression(methodToInvoke, SH.CallArguments(msgArgument, tokenArgument)));
                }
                else
                {
                    retType = SF.ParseTypeName(Names.SystemTask);

                    return SF.ReturnStatement(
                        SF.InvocationExpression(methodToInvoke, SH.CallArguments(msgArgument, tokenArgument)));
                }
            }
            else
            {
                if (isTry)
                {
                    retType = SF.ParseTypeName(Names.RpcResultStruct.Full);

                    var baseMethodInvokeExp = SF.InvocationExpression(methodToInvoke, SH.CallArguments(msgArgument, tokenArgument));
                    return SF.ReturnStatement(SH.MemberOf(baseMethodInvokeExp, "Result"));
                }
                else
                {
                    retType = SF.PredefinedType(SF.Token(SyntaxKind.VoidKeyword));

                    var baseMethodInvokeExp = SF.InvocationExpression(methodToInvoke, SH.CallArguments(msgArgument, tokenArgument));
                    return SF.ExpressionStatement(SF.InvocationExpression(SH.MemberOf(baseMethodInvokeExp, "Wait")));
                }
            }
        }

        private StatementSyntax GenerateRemoteCallStatement(TypeString respMessageType, string returnDataType, bool isAsync, bool isTry, out TypeSyntax retType)
        {
            var msgArgument = SF.Argument(SF.IdentifierName("message"));

            var methodName = isTry ? "TryCallAsync" : "CallAsync";
            var methodToInvoke = SH.GenericName(methodName, returnDataType, respMessageType.Full);
            var tokenArgument = SF.Argument(SF.IdentifierName("cancelToken"));

            if (isAsync)
            {
                if (isTry)
                {
                    retType = SH.GenericType(Names.SystemTask, SH.GenericType(Names.RpcResultStruct.Full, returnDataType));

                    return SF.ReturnStatement(
                        SF.InvocationExpression(methodToInvoke, SH.CallArguments(msgArgument, tokenArgument)));
                }
                else
                {
                    retType = SH.GenericType(Names.SystemTask, returnDataType);

                    return SF.ReturnStatement(
                        SF.InvocationExpression(methodToInvoke, SH.CallArguments(msgArgument, tokenArgument)));
                }
            }
            else
            {
                if (isTry)
                {
                    retType = SH.GenericName(Names.RpcResultStruct.Full, returnDataType);

                    var baseMethodInvokeExp = SF.InvocationExpression(methodToInvoke, SH.CallArguments(msgArgument, tokenArgument));
                    return SF.ReturnStatement(SH.MemberOf(baseMethodInvokeExp, "Result"));
                }
                else
                {
                    retType = SF.ParseTypeName(returnDataType);

                    var baseMethodInvokeExp = SF.InvocationExpression(methodToInvoke, SH.CallArguments(msgArgument, tokenArgument));
                    return SF.ReturnStatement(SH.MemberOf(baseMethodInvokeExp, "Result"));
                }
            }
        }

        private static TypeSyntax GetTypeSyntax(ParamDeclaration param)
        {
            if (param == null || param.ParamType == null)
                return SF.PredefinedType(SF.Token(SyntaxKind.VoidKeyword));
            else
                return SF.ParseTypeName(param.ParamType);
        }

        private string GetFacadeClassName(bool isAsync, bool isTry)
        {
            return AddAsyncTrySuffix("Facade", isAsync, isTry);
        }

        private string GetFacadePropertyName(bool isAsync, bool isTry)
        {
            return AddAsyncTrySuffix("", isAsync, isTry);
        }

        private string AddAsyncTrySuffix(string name, bool isAsync, bool isTry)
        {
            if (isAsync)
                name = "Async" + name;

            if (isTry)
                name = "Try" + name;

            return name;
        }

        internal static ExpressionSyntax GenerateStreamFactoryCreationExp(TypeString factoryClassName)
        {
            return SF.ObjectCreationExpression(SH.FullTypeName(factoryClassName))
                .WithoutArguments();
        }
    }
}
