using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using SH = SharpRpc.Builder.SyntaxHelper;

namespace SharpRpc.Builder
{
    internal class ServerStubBuilder
    {
        private readonly ContractDeclaration _contract;

        public ServerStubBuilder(ContractDeclaration contract)
        {
            _contract = contract;
        }

        public ClassDeclarationSyntax GenerateCode()
        {
            var contractType = _contract.InterfaceName;
            var serverStubType = new TypeString(contractType.Namespace, "Service");

            return SF.ClassDeclaration(serverStubType.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword), SF.Token(SyntaxKind.AbstractKeyword))
                .AddBaseListTypes(SF.SimpleBaseType(SF.ParseTypeName(Names.RpcServiceBaseClass.Full)))
                .AddMembers(GenerateRpcMethods(serverStubType))
                .AddMembers(GenerateOnMessageOverride(), GenerateOnRequestOverride());
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

            var retStatement = SF.ReturnStatement(
                SF.ObjectCreationExpression(SH.FullTypeName(Names.ServiceBindingClass))
                .AddArgumentListArguments(
                    SH.IdentifierArgument("serviceImplFactory"),
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

        private MethodDeclarationSyntax[] GenerateRpcMethods(TypeString clientStubTypeName)
        {
            var methods = new List<MethodDeclarationSyntax>();

            foreach (var callDec in _contract.Calls)
            {
                methods.Add(GenerateStubMethod(callDec));

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
            StatementSyntax catchStatement;

            if (callDec.ReturnsData)
            {
                handlerInvokationStatement = SH.VarDeclaration("result", handlerInvokationExp);
                catchStatement = SF.ReturnStatement();
            }
            else
            {
                handlerInvokationStatement = SF.ExpressionStatement(handlerInvokationExp);
                catchStatement = SF.ReturnStatement(
                    SH.InvocationExpression(Names.ServiceCreateFaultResponseMethod, SH.IdentifierArgument("ex")));
            }

            var respStatements = GenerateResponseCreationStatements(callDec).ToArray();

            var defaultCatch = SF.CatchClause(SF.CatchDeclaration(SF.ParseTypeName(Names.SystemException), SF.Identifier("ex")),
                null, SF.Block(catchStatement));

            var tryCtachBlock = SF.TryStatement()
                .AddCatches(defaultCatch)
                .WithBlock(SF.Block(handlerInvokationStatement).AddStatements(respStatements));

            var retType = SH.GenericType(Names.SystemValueTask, Names.ResponseInterface.Full);

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

            foreach (var call in _contract.Calls)
            {
                if (call.CallType == ContractCallType.ClientMessage)
                {
                    var messageType = _contract.GetOnWayMessageClassName(call.MethodName);
                    var typedMessageVarName = "m" + index++;

                    var isExpression = SF.IsPatternExpression(SF.IdentifierName("message"),
                        SF.DeclarationPattern(SF.ParseTypeName(messageType.Full), SF.SingleVariableDesignation(SF.Identifier(typedMessageVarName))));

                    var rpcMethodCall = SF.ReturnStatement(GenerateRpcInvocation(call, typedMessageVarName));

                    ifRoot = SF.IfStatement(isExpression, rpcMethodCall, SF.ElseClause(ifRoot));
                }
            }

            var method = SF.MethodDeclaration(SF.ParseTypeName(Names.SystemValueTask), Names.RpcServiceBaseOnMessageMethod)
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

            foreach (var call in _contract.Calls)
            {
                if (call.CallType == ContractCallType.ClientCall)
                {
                    var messageType = _contract.GetRequestClassName(call.MethodName);
                    var typedMessageVarName = "r" + index++;

                    var isExpression = SF.IsPatternExpression(SF.IdentifierName("request"),
                        SF.DeclarationPattern(SF.ParseTypeName(messageType.Full), SF.SingleVariableDesignation(SF.Identifier(typedMessageVarName))));

                    var methodToInvoke = "Invoke" + call.MethodName;

                    var rpcMethodCall = SF.ReturnStatement(
                        SF.InvocationExpression(SF.IdentifierName(methodToInvoke))
                        .WithArguments(SH.IdentifierArgument(typedMessageVarName)));

                    ifRoot = SF.IfStatement(isExpression, rpcMethodCall, SF.ElseClause(ifRoot));
                }
            }

            var retType = SH.GenericType(Names.SystemValueTask, Names.ResponseInterface.Full);

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

            return SF.InvocationExpression(SF.IdentifierName(methodName), SH.CallArguments(args));
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
                return SF.ParseTypeName(Names.SystemValueTask);
            else
                return SH.GenericType(Names.SystemValueTask, param.ParamType);
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
