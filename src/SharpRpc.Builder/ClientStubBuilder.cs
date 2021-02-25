using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Text;
using Sf = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Sh = SharpRpc.Builder.SyntaxHelper;

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
            var compUnit = SyntaxFactory.CompilationUnit();
            var stubNamespace = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.IdentifierName(contractType.Namespace));

            var constructorInitializer = SyntaxFactory.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
                .AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("endpoint")));

            var constructor = SyntaxFactory.ConstructorDeclaration(clientStubType.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(SyntaxHelper.Parameter("endpoint", Names.RpcClientEndpointBaseClass.Full))
                .WithInitializer(constructorInitializer)
                .WithBody(SyntaxFactory.Block());

            var stubClassDeclaration = SyntaxFactory.ClassDeclaration(clientStubType.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(Names.RpcClientBaseClass.Full)))
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
                var paramSyntax = Sf
                    .Parameter(Sf.Identifier(param.ParamName))
                    .WithType(GetTypeSyntax(param));

                methodParams.Add(paramSyntax);
            }

            var msgTypeName = Names.GetOnWayMessageName(_contract.InterfaceName.Short, callDec.MethodName);

            var msgCreateClause = Sf.EqualsValueClause(
                Sf.ObjectCreationExpression(Sf.ParseTypeName(msgTypeName))
                .WithArgumentList(Sf.ArgumentList()));

            bodyStatements.Add(Sh.VariableDeclaration(msgTypeName, "message", msgCreateClause));

            foreach (var paramDec in callDec.Params)
            {
                bodyStatements.Add(Sh.AssignmentStatement(
                    Sh.VarPropertyAccess("message", paramDec.MessagePropertyName),
                    Sf.IdentifierName(paramDec.ParamName)));
            }

            bodyStatements.Add(GenerateSendMessageInvoke(isAsync, isTry, out var retType));

            var methodName = callDec.MethodName;

            if (isAsync)
                methodName += "Async";

            if (isTry)
                methodName = "Try" + methodName;

            var method = Sf.MethodDeclaration(retType, methodName)
                .AddModifiers(Sf.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(methodParams.ToArray())
                .WithBody(Sf.Block(bodyStatements));

            return method;
        }

        private StatementSyntax GenerateSendMessageInvoke(bool isAsync, bool isTry, out TypeSyntax retType)
        {
            var msgArgument = Sf.Argument(Sf.IdentifierName("message"));

            if (isAsync)
            {
                if (isTry)
                {
                    retType = Sh.GenericType(Names.SystemValueTask, Names.RpcResultStruct.Full);

                    return Sf.ReturnStatement(
                        Sf.InvocationExpression(Sf.IdentifierName("TrySendMessageAsync"), Sh.CallArguments(msgArgument)));
                }
                else
                {
                    retType = Sf.ParseTypeName(Names.SystemValueTask);

                    return Sf.ReturnStatement(
                        Sf.InvocationExpression(Sf.IdentifierName("SendMessageAsync"), Sh.CallArguments(msgArgument)));
                }
            }
            else
            {
                if (isTry)
                {
                    retType = Sf.ParseTypeName(Names.RpcResultStruct.Full);

                    return Sf.ReturnStatement(
                        Sf.InvocationExpression(Sf.IdentifierName("TrySendMessage"), Sh.CallArguments(msgArgument)));
                }
                else
                {
                    retType = Sf.PredefinedType(Sf.Token(SyntaxKind.VoidKeyword));

                    return Sh.ThisCallStatement("SendMessage", Sh.IdentifierArgument("message"));
                }
            }
        }

        private TypeSyntax GetTypeSyntax(ParamDeclaration param)
        {
            if (param == null || param.ParamType == null)
                return Sf.PredefinedType(Sf.Token(SyntaxKind.VoidKeyword));
            else
                return Sf.ParseTypeName(param.ParamType);
        }
    }
}
