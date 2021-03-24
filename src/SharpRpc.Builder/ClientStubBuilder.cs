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
    internal class ClientStubBuilder
    {
        private readonly ContractDeclaration _contract;

        public ClientStubBuilder(ContractDeclaration contract)
        {
            _contract = contract;
        }

        public ClassDeclarationSyntax GenerateCode()
        {
            var clientStubType = _contract.ClientStubClassName;

            var constructorInitializer = SF.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
                .AddArgumentListArguments(SH.IdentifierArgument("endpoint"), SH.IdentifierArgument("serializer"));

            var endpointConsParam = SH.Parameter("endpoint", Names.RpcClientEndpointBaseClass.Full);
            var serializerConsParam = SH.Parameter("serializer", Names.RpcSerializerInterface.Full);

            var constructor = SF.ConstructorDeclaration(clientStubType.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(endpointConsParam, serializerConsParam)
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
            var serializerCreateClause = SH.InvocationExpression(Names.FacadeSerializerAdapterFactoryMethod, SF.Argument(SF.IdentifierName("serializer")));
            var serializerVarStatement = SH.VarDeclaration("adapter", serializerCreateClause);

            var clientCreateExpression = SF.ObjectCreationExpression(SH.ShortTypeName(_contract.ClientStubClassName))
                .WithArgumentList(SH.CallArguments(SH.IdentifierArgument("endpoint"), SH.IdentifierArgument("adapter")));

            var returnStatement = SF.ReturnStatement(clientCreateExpression);

            var endpointParam = SH.Parameter("endpoint", Names.RpcClientEndpointBaseClass.Full);

            var serializerDefValue = SH.EnumValue(Names.SerializerChoiceEnum.Full, _contract.GetDefaultSerializerChoice());
            var serializerParam = SH.Parameter("serializer", Names.SerializerChoiceEnum.Full)
                .WithDefault(SF.EqualsValueClause(serializerDefValue));

            return SF.MethodDeclaration(SF.ParseTypeName(_contract.ClientStubClassName.Short), "CreateClient")
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword), SF.Token(SyntaxKind.StaticKeyword))
                .AddParameterListParameters(endpointParam, serializerParam)
                .WithBody(SF.Block(serializerVarStatement, returnStatement));
        }

        private MethodDeclarationSyntax[] GenerateCallMethods(TypeString clientStubTypeName)
        {
            var methods = new List<MethodDeclarationSyntax>();

            foreach (var callDec in _contract.Calls)
            {
                if (callDec.CallType == ContractCallType.ClientMessage)
                {
                    methods.Add(GenerateOneWayCall(callDec, clientStubTypeName, false, false));
                    methods.Add(GenerateOneWayCall(callDec, clientStubTypeName, true, false));
                    methods.Add(GenerateOneWayCall(callDec, clientStubTypeName, false, true));
                    methods.Add(GenerateOneWayCall(callDec, clientStubTypeName, true, true));
                }
                else if (callDec.CallType == ContractCallType.ClientCall)
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

        private List<ParameterSyntax> GenerateMethodParams(CallDeclaration callDec)
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

        private IEnumerable<StatementSyntax> GenerateCreateAndFillMessageStatements(CallDeclaration callDec, TypeString msgClassName)
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
                    retType = SH.GenericType(Names.SystemValueTask, Names.RpcResultStruct.Full);

                    return SF.ReturnStatement(
                        SF.InvocationExpression(SF.IdentifierName("TrySendMessageAsync"), SH.CallArguments(msgArgument)));
                }
                else
                {
                    retType = SF.ParseTypeName(Names.SystemValueTask);

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

            var methodToInvoke = isTry ? SH.GenericName("TryCallAsync", respMessageType.Full)
                : SH.GenericName("CallAsync", respMessageType.Full);

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

            if (isAsync)
            {
                if (isTry)
                {
                    retType = SH.GenericType(Names.SystemValueTask, Names.RpcResultStruct.Full);

                    return SF.ReturnStatement(
                        SF.InvocationExpression(SF.IdentifierName("TrySendMessageAsync"), SH.CallArguments(msgArgument)));
                }
                else
                {
                    retType = SF.ParseTypeName(Names.SystemValueTask);

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

        private TypeSyntax GetTypeSyntax(ParamDeclaration param)
        {
            if (param == null || param.ParamType == null)
                return SF.PredefinedType(SF.Token(SyntaxKind.VoidKeyword));
            else
                return SF.ParseTypeName(param.ParamType);
        }
    }
}
