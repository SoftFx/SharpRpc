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

        public ClassDeclarationSyntax GenerateCode()
        {
            if (_isCallback)
                return GenerateServerSideStub();
            else
                return GenerateClientSideStub();
        }

        private ClassDeclarationSyntax GenerateClientSideStub()
        {
            var clientStubType = _contract.ClientStubClassName;
            var addHandlerParam = !_isCallback && _contract.HasCallbacks;

            var constructorInitializer = SF.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
                .AddArgumentListArguments(SH.IdentifierArgument("endpoint"), SH.IdentifierArgument("descriptor"));

            if (addHandlerParam)
                constructorInitializer = constructorInitializer.AddArgumentListArguments(SH.IdentifierArgument("callbackHandler"));

            var endpointConsParam = SH.Parameter("endpoint", Names.RpcClientEndpointBaseClass.Full);
            var descriptorConsParam = SH.Parameter("descriptor", Names.ContractDescriptorClass.Full);

            var constructor = SF.ConstructorDeclaration(clientStubType.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(endpointConsParam, descriptorConsParam)
                .WithInitializer(constructorInitializer)
                .WithBody(SF.Block());

            if (addHandlerParam)
            {
                var handlerType = SH.ShortTypeName(_contract.CallbackServiceStubClassName);
                var handlerConsParam = SH.Parameter("callbackHandler", handlerType);
                constructor = constructor.AddParameterListParameters(handlerConsParam);
            }

            return SF.ClassDeclaration(clientStubType.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SF.SimpleBaseType(SF.ParseTypeName(Names.RpcClientBaseClass.Full)))
                .AddMembers(constructor)
                .AddMembers(GenerateCallMethods(clientStubType));
        }

        private ClassDeclarationSyntax GenerateServerSideStub()
        {
            var clientStubType = _contract.CallbackClientStubClassName;

            var constructorInitializer = SF.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
                .AddArgumentListArguments(SH.IdentifierArgument("channel"));

            var channelConsParam = SH.Parameter("channel", Names.RpcChannelClass.Full);

            var constructor = SF.ConstructorDeclaration(clientStubType.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(channelConsParam)
                .WithInitializer(constructorInitializer)
                .WithBody(SF.Block());

            return SF.ClassDeclaration(clientStubType.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SF.SimpleBaseType(SF.ParseTypeName(Names.RpcClientBaseClass.Full)))
                .AddMembers(constructor)
                .AddMembers(GenerateCallMethods(clientStubType));
        }

        public MethodDeclarationSyntax GenerateFactoryMethod()
        {
            var addHandlerParam = !_isCallback && _contract.HasCallbacks;

            var serializerCreateClause = SH.InvocationExpression(Names.FacadeSerializerAdapterFactoryMethod, SF.Argument(SF.IdentifierName("serializer")));
            var serializerVarStatement = SH.VarDeclaration("adapter", serializerCreateClause);

            var descriptorVarStatement = SH.VarDeclaration("descriptor",
                SH.InvocationExpression(Names.FacadeCreateDescriptorMethod, SH.IdentifierArgument("adapter")));

            var clientCreateExpression = SF.ObjectCreationExpression(SH.ShortTypeName(_contract.ClientStubClassName))
                .WithArgumentList(SH.CallArguments(SH.IdentifierArgument("endpoint"), SH.IdentifierArgument("descriptor")));

            if (addHandlerParam)
                clientCreateExpression = clientCreateExpression.AddArgumentListArguments(SH.IdentifierArgument("callbackHandler"));

            var returnStatement = SF.ReturnStatement(clientCreateExpression);

            var endpointParam = SH.Parameter("endpoint", Names.RpcClientEndpointBaseClass.Full);

            var serializerDefValue = SH.EnumValue(Names.SerializerChoiceEnum.Full, _contract.GetDefaultSerializerChoice());
            var serializerParam = SH.Parameter("serializer", Names.SerializerChoiceEnum.Full)
                .WithDefault(SF.EqualsValueClause(serializerDefValue));

            var paramList = new List<ParameterSyntax>();
            paramList.Add(endpointParam);

            if (addHandlerParam)
            {
                var handlerType = SH.ShortTypeName(_contract.CallbackServiceStubClassName);
                paramList.Add(SH.Parameter("callbackHandler", handlerType));
            }

            paramList.Add(serializerParam);

            return SF.MethodDeclaration(SF.ParseTypeName(_contract.ClientStubClassName.Short), "CreateClient")
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword), SF.Token(SyntaxKind.StaticKeyword))
                .AddParameterListParameters(paramList.ToArray())
                .WithBody(SF.Block(serializerVarStatement, descriptorVarStatement, returnStatement));
        }

        private MethodDeclarationSyntax[] GenerateCallMethods(TypeString clientStubTypeName)
        {
            var methods = new List<MethodDeclarationSyntax>();

            foreach (var callDec in _contract.Calls)
            {
                bool addMessage = _isCallback ? (callDec.CallType == ContractCallType.MessageToClient) : (callDec.CallType == ContractCallType.MessageToServer);
                bool addCall = _isCallback ? (callDec.CallType == ContractCallType.CallToClient) : (callDec.CallType == ContractCallType.CallToServer);

                if (addMessage)
                {
                    methods.Add(GenerateOneWayCall(callDec, clientStubTypeName, false, false));
                    methods.Add(GenerateOneWayCall(callDec, clientStubTypeName, true, false));
                    methods.Add(GenerateOneWayCall(callDec, clientStubTypeName, false, true));
                    methods.Add(GenerateOneWayCall(callDec, clientStubTypeName, true, true));

                    if (callDec.EnablePrebuild)
                    {
                        methods.Add(GeneratePrebuiltMessageSender(callDec, clientStubTypeName, false, false));
                        methods.Add(GeneratePrebuiltMessageSender(callDec, clientStubTypeName, true, false));
                        methods.Add(GeneratePrebuiltMessageSender(callDec, clientStubTypeName, false, true));
                        methods.Add(GeneratePrebuiltMessageSender(callDec, clientStubTypeName, true, true));
                    }
                }
                else if (addCall)
                {
                    methods.Add(GenerateCall(callDec, clientStubTypeName, false, false));
                    methods.Add(GenerateCall(callDec, clientStubTypeName, true, false));
                    methods.Add(GenerateCall(callDec, clientStubTypeName, false, true));
                    methods.Add(GenerateCall(callDec, clientStubTypeName, true, true));
                }
            }

            return methods.ToArray();
        }

        private MethodDeclarationSyntax GenerateOneWayCall(CallDeclaration callDec, TypeString clientStubTypeName, bool isAsync, bool isTry)
        {            
            var bodyStatements = new List<StatementSyntax>();
            var methodParams = GenerateMethodParams(callDec);

            var msgClassName = _contract.GetOnWayMessageClassName(callDec.MethodName);

            bodyStatements.AddRange(GenerateCreateAndFillMessageStatements(callDec, msgClassName));
            bodyStatements.Add(GenerateSendMessageStatement(isAsync, isTry, out var retType));

            var methodName = AtttributeMethodName(callDec, isAsync, isTry);

            var method = SF.MethodDeclaration(retType, methodName)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(methodParams.ToArray())
                .WithBody(SF.Block(bodyStatements));

            return method;
        }

        private MethodDeclarationSyntax GeneratePrebuiltMessageSender(CallDeclaration callDec, TypeString clientStubTypeName, bool isAsync, bool isTry)
        {
            var bodyStatements = new List<StatementSyntax>();
            var msgClassName = _contract.GetPrebuiltMessageClassName(callDec.MethodName);
            var msgParam = SH.Parameter("message", SH.FullTypeName(msgClassName));


            //bodyStatements.AddRange(GenerateCreateAndFillMessageStatements(callDec, msgClassName));
            bodyStatements.Add(GenerateSendMessageStatement(isAsync, isTry, out var retType));

            var methodName = AtttributeMethodName(callDec, isAsync, isTry);

            var method = SF.MethodDeclaration(retType, methodName)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(msgParam)
                .WithBody(SF.Block(bodyStatements));

            return method;
        }

        private MethodDeclarationSyntax GenerateCall(CallDeclaration callDec, TypeString clientStubTypeName, bool isAsync, bool isTry)
        {
            var bodyStatements = new List<StatementSyntax>();
            var methodParams = GenerateMethodParams(callDec);

            var requestMsgClassName = _contract.GetRequestClassName(callDec.MethodName);
            var responseMsgClassName = _contract.GetResponseClassName(callDec.MethodName);

            bodyStatements.AddRange(GenerateCreateAndFillMessageStatements(callDec, requestMsgClassName));

            TypeSyntax methodRetType;

            if (!callDec.ReturnsData)
                bodyStatements.Add(GenerateRemoteCallStatement(responseMsgClassName, isAsync, isTry, out methodRetType));
            else
                bodyStatements.Add(GenerateRemoteCallStatement(responseMsgClassName, callDec.ReturnParam.ParamType, isAsync, isTry, out methodRetType));

            var methodName = AtttributeMethodName(callDec, isAsync, isTry);

            var method = SF.MethodDeclaration(methodRetType, methodName)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(methodParams.ToArray())
                .WithBody(SF.Block(bodyStatements));

            return method;
        }

        private string AtttributeMethodName(CallDeclaration callDec, bool isAsync, bool isTry)
        {
            var methodName = callDec.MethodName;

            if (isAsync)
                methodName += "Async";

            if (isTry)
                methodName = "Try" + methodName;

            return methodName;
        }

        internal static List<ParameterSyntax> GenerateMethodParams(CallDeclaration callDec)
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

        internal static IEnumerable<StatementSyntax> GenerateCreateAndFillMessageStatements(CallDeclaration callDec, TypeString msgClassName)
        {
            var msgCreateClause = SF.EqualsValueClause(
                SF.ObjectCreationExpression(SF.ParseTypeName(msgClassName.Full))
                .WithArgumentList(SF.ArgumentList()));

            yield return SH.VariableDeclaration(msgClassName.Full, "message", msgCreateClause);

            foreach (var paramDec in callDec.Params)
            {
                yield return SH.AssignmentStatement(
                    SH.MemeberOfIdentifier("message", paramDec.MessagePropertyName),
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

            var methodName = isTry ? "TryCallAsync" : "CallAsync";
            var methodToInvoke = SH.GenericName(methodName, respMessageType.Full);

            if (isAsync)
            {
                if (isTry)
                {
                    retType = SH.GenericType(Names.SystemTask, Names.RpcResultStruct.Full);

                    return SF.ReturnStatement(
                        SF.InvocationExpression(methodToInvoke, SH.CallArguments(msgArgument)));
                }
                else
                {
                    retType = SF.ParseTypeName(Names.SystemTask);

                    return SF.ReturnStatement(
                        SF.InvocationExpression(methodToInvoke, SH.CallArguments(msgArgument)));
                }
            }
            else
            {
                if (isTry)
                {
                    retType = SF.ParseTypeName(Names.RpcResultStruct.Full);

                    var baseMethodInvokeExp = SF.InvocationExpression(methodToInvoke, SH.CallArguments(msgArgument));
                    return SF.ReturnStatement(SH.MemberOf(baseMethodInvokeExp, "Result"));
                }
                else
                {
                    retType = SF.PredefinedType(SF.Token(SyntaxKind.VoidKeyword));

                    var baseMethodInvokeExp = SF.InvocationExpression(methodToInvoke, SH.CallArguments(msgArgument));
                    return SF.ExpressionStatement(SF.InvocationExpression(SH.MemberOf(baseMethodInvokeExp, "Wait")));
                }
            }
        }

        private StatementSyntax GenerateRemoteCallStatement(TypeString respMessageType, string returnDataType, bool isAsync, bool isTry, out TypeSyntax retType)
        {
            var msgArgument = SF.Argument(SF.IdentifierName("message"));

            var methodName = isTry ? "TryCallAsync" : "CallAsync";
            var methodToInvoke = SH.GenericName(methodName, returnDataType, respMessageType.Full);

            if (isAsync)
            {
                if (isTry)
                {
                    retType = SH.GenericType(Names.SystemTask, SH.GenericType(Names.RpcResultStruct.Full, returnDataType));

                    return SF.ReturnStatement(
                        SF.InvocationExpression(methodToInvoke, SH.CallArguments(msgArgument)));
                }
                else
                {
                    retType = SH.GenericType(Names.SystemTask, returnDataType);

                    return SF.ReturnStatement(
                        SF.InvocationExpression(methodToInvoke, SH.CallArguments(msgArgument)));
                }

                //if (isTry)
                //{
                //    retType = SH.GenericType(Names.SystemValueTask, Names.RpcResultStruct.Full);

                //    return SF.ReturnStatement(
                //        SF.InvocationExpression(SF.IdentifierName("TrySendMessageAsync"), SH.CallArguments(msgArgument)));
                //}
                //else
                //{
                //    retType = SF.ParseTypeName(Names.SystemValueTask);

                //    return SF.ReturnStatement(
                //        SF.InvocationExpression(SF.IdentifierName("SendMessageAsync"), SH.CallArguments(msgArgument)));
                //}
            }
            else
            {
                if (isTry)
                {
                    retType = SH.GenericName(Names.RpcResultStruct.Full, returnDataType);

                    var baseMethodInvokeExp = SF.InvocationExpression(methodToInvoke, SH.CallArguments(msgArgument));
                    return SF.ReturnStatement(SH.MemberOf(baseMethodInvokeExp, "Result"));
                }
                else
                {
                    retType = SF.ParseTypeName(returnDataType);

                    var baseMethodInvokeExp = SF.InvocationExpression(methodToInvoke, SH.CallArguments(msgArgument));
                    return SF.ReturnStatement(SH.MemberOf(baseMethodInvokeExp, "Result"));
                }

                //if (isTry)
                //{
                //    retType = SF.ParseTypeName(Names.RpcResultStruct.Full);

                //    return SF.ReturnStatement(
                //        SF.InvocationExpression(SF.IdentifierName("TrySendMessage"), SH.CallArguments(msgArgument)));
                //}
                //else
                //{
                //    retType = SF.PredefinedType(SF.Token(SyntaxKind.VoidKeyword));

                //    return SH.ThisCallStatement("SendMessage", SH.IdentifierArgument("message"));
                //}
            }
        }

        private static TypeSyntax GetTypeSyntax(ParamDeclaration param)
        {
            if (param == null || param.ParamType == null)
                return SF.PredefinedType(SF.Token(SyntaxKind.VoidKeyword));
            else
                return SF.ParseTypeName(param.ParamType);
        }
    }
}
