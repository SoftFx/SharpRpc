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

        public void GenerateCode(GeneratorExecutionContext context)
        {
            var contractType = _contract.InterfaceName;
            var clientStubType = new TypeString(contractType.Namespace, contractType.Short + "_Client");
            var compUnit = SF.CompilationUnit();
            var stubNamespace = SF.NamespaceDeclaration(SF.IdentifierName(contractType.Namespace));

            var constructorInitializer = SF.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
                .AddArgumentListArguments(SF.Argument(SF.IdentifierName("endpoint")));

            var constructor = SF.ConstructorDeclaration(clientStubType.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(SyntaxHelper.Parameter("endpoint", Names.RpcClientEndpointBaseClass.Full))
                .WithInitializer(constructorInitializer)
                .WithBody(SF.Block());

            var stubClassDeclaration = SF.ClassDeclaration(clientStubType.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SF.SimpleBaseType(SF.ParseTypeName(Names.RpcClientBaseClass.Full)))
                .AddMembers(constructor);

            stubClassDeclaration = stubClassDeclaration.AddMembers(GenerateCallMethods(clientStubType));

            stubNamespace = stubNamespace.AddMembers(stubClassDeclaration);
            compUnit = compUnit.AddMembers(stubNamespace);

            var srcCode = compUnit
                .NormalizeWhitespace()
                .ToFullString();

            context.AddSource(clientStubType.Full, SourceText.From(srcCode, Encoding.UTF8));
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
            }

            return methods.ToArray();
        }

        private MethodDeclarationSyntax GenerateOneWayCall(CallDeclaration callDec, TypeString clientStubTypeName, bool isAsync, bool isTry)
        {
            var methodParams = new List<ParameterSyntax>();
            var bodyStatements = new List<StatementSyntax>();

            foreach (var param in callDec.Params)
            {
                var paramSyntax = SF
                    .Parameter(SF.Identifier(param.ParamName))
                    .WithType(GetTypeSyntax(param));

                methodParams.Add(paramSyntax);
            }

            var msgTypeName = _contract.GetOnWayMessageClassName(callDec.MethodName);

            var msgCreateClause = SF.EqualsValueClause(
                SF.ObjectCreationExpression(SF.ParseTypeName(msgTypeName.Full))
                .WithArgumentList(SF.ArgumentList()));

            bodyStatements.Add(SH.VariableDeclaration(msgTypeName.Full, "message", msgCreateClause));

            foreach (var paramDec in callDec.Params)
            {
                bodyStatements.Add(SH.AssignmentStatement(
                    SH.MemeberOfIdentifier("message", paramDec.MessagePropertyName),
                    SF.IdentifierName(paramDec.ParamName)));
            }

            bodyStatements.Add(GenerateSendMessageInvoke(isAsync, isTry, out var retType));

            var methodName = callDec.MethodName;

            if (isAsync)
                methodName += "Async";

            if (isTry)
                methodName = "Try" + methodName;

            var method = SF.MethodDeclaration(retType, methodName)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(methodParams.ToArray())
                .WithBody(SF.Block(bodyStatements));

            return method;
        }

        private StatementSyntax GenerateSendMessageInvoke(bool isAsync, bool isTry, out TypeSyntax retType)
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
