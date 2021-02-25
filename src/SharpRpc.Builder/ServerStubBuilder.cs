using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
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

        public void GenerateCode(GeneratorExecutionContext context)
        {
            var contractType = _contract.InterfaceName;
            var serverStubType = new TypeString(contractType.Namespace, contractType.Short + "_Service");

            //var constructorInitializer = Sf.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
            //    .AddArgumentListArguments(Sf.Argument(SyntaxFactory.IdentifierName("endpoint")));

            //var constructor = Sf.ConstructorDeclaration(clientStubType.Short)
            //    .AddModifiers(Sf.Token(SyntaxKind.PublicKeyword))
            //    .AddParameterListParameters(SyntaxHelper.Parameter("endpoint", Names.RpcClientEndpointBaseClass.Full))
            //    .WithInitializer(constructorInitializer)
            //    .WithBody(Sf.Block());

            var stubClassDeclaration = SF.ClassDeclaration(serverStubType.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword), SF.Token(SyntaxKind.AbstractKeyword))
                .AddBaseListTypes(SF.SimpleBaseType(SF.ParseTypeName(Names.RpcServiceBaseClass.Full)))
                .AddMembers(GenerateRpcMethods(serverStubType))
                .AddMembers(GenerateOnMessageOverride());

            var stubNamespace = SF.NamespaceDeclaration(SF.IdentifierName(contractType.Namespace))
                .AddMembers(stubClassDeclaration);

            var compUnit = SF.CompilationUnit()
                .AddMembers(stubNamespace);

            var srcCode = compUnit
                .NormalizeWhitespace()
                .ToFullString();

            context.AddSource(serverStubType.Full, SourceText.From(srcCode, Encoding.UTF8));
        }

        private MethodDeclarationSyntax[] GenerateRpcMethods(TypeString clientStubTypeName)
        {
            var methods = new List<MethodDeclarationSyntax>();

            foreach (var callDec in _contract.Calls)
                methods.Add(GenerateStubMethod(callDec));

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

            var methodName = callDec.MethodName + "Async";
            var retType = GetTaskOf(callDec.ReturnParam);

            var method = SF.MethodDeclaration(retType, methodName)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword), SF.Token(SyntaxKind.AbstractKeyword))
                .AddParameterListParameters(methodParams.ToArray())
                .WithoutBody();

            return method;
        }

        private MethodDeclarationSyntax GenerateOnMessageOverride()
        {
            StatementSyntax ifRoot = SF.ReturnStatement(
                SH.ThisInvocation(Names.RpcServiceBasOnUnknownMessage, SH.IdentifierArgument("message")));

            var index = 0;

            foreach (var call in _contract.Calls)
            {
                if (call.CallType == ContractCallType.ClientMessage)
                {
                    var messageType = Names.GetOnWayMessageName(_contract.InterfaceName.Full, call.MethodName);
                    var typedMessageVarName = "m" + index++;

                    var isExpression = SF.IsPatternExpression(SF.IdentifierName("message"),
                        SF.DeclarationPattern(SF.ParseTypeName(messageType), SF.SingleVariableDesignation(SF.Identifier(typedMessageVarName))));

                    var rpcMethodCall = SF.ReturnStatement(GenerateRpcInvocation(call, typedMessageVarName));

                    ifRoot = SF.IfStatement(isExpression, rpcMethodCall, SF.ElseClause(ifRoot));
                }
            }

            var method = SF.MethodDeclaration(SF.ParseTypeName(Names.SystemTask), Names.RpcServiceBaseOnMessageMethod)
               .AddModifiers(SF.Token(SyntaxKind.ProtectedKeyword), SF.Token(SyntaxKind.OverrideKeyword))
               .AddParameterListParameters(SH.Parameter("message", Names.RpcMessageInterface.Full))
               .WithBody(SF.Block(ifRoot));

            return method;
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

            var methodName = callDec.MethodName + "Async";

            return SF.InvocationExpression(SF.IdentifierName(methodName), SH.CallArguments(args));
        }

        private TypeSyntax GetTaskOf(ParamDeclaration param)
        {
            if (param == null || param.ParamType == null)
                return SF.ParseTypeName(Names.SystemTask);
            else
                return SH.GenericType(Names.SystemTask, param.ParamType);
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
