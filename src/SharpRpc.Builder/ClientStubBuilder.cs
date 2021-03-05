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

            var constructorInitializer = SF.ConstructorInitializer(SyntaxKind.BaseConstructorInitializer)
                .AddArgumentListArguments(SH.IdentifierArgument("endpoint"), SH.IdentifierArgument("serializer"));

            var endpointConsParam = SH.Parameter("endpoint", Names.RpcClientEndpointBaseClass.Full);
            var serializerConsParam = SH.Parameter("serializer", Names.RpcSerializerInterface.Full);

            var constructor = SF.ConstructorDeclaration(clientStubType.Short)
                .AddModifiers(SF.Token(SyntaxKind.ProtectedKeyword))
                .AddParameterListParameters(endpointConsParam, serializerConsParam)
                .WithInitializer(constructorInitializer)
                .WithBody(SF.Block());

            var stubClassDeclaration = SF.ClassDeclaration(clientStubType.Short)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SF.SimpleBaseType(SF.ParseTypeName(Names.RpcClientBaseClass.Full)))
                .AddMembers(constructor)
                .AddMembers(GenerateFactories(clientStubType))
                .AddMembers(GenerateCallMethods(clientStubType));

            var stubNamespace = SF.NamespaceDeclaration(SF.IdentifierName(contractType.Namespace))
                .AddMembers(stubClassDeclaration);

            var compUnit = SF.CompilationUnit()
                .AddMembers(stubNamespace);

            var srcCode = compUnit
                .NormalizeWhitespace()
                .ToFullString();

            context.AddSource(clientStubType.Full, SourceText.From(srcCode, Encoding.UTF8));
        }

        private MethodDeclarationSyntax[] GenerateFactories(TypeString stubClassName)
        {
            var factoryMethods = new List<MethodDeclarationSyntax>();

            if (_contract.Serializers.Count > 1)
            {
                foreach (var serializerEntry in _contract.Serializers)
                {
                    var serializerTypeName = serializerEntry.AdapterClassName;
                    var serializerName = serializerEntry.Builder.Name;

                    factoryMethods.Add(GenerateFactoryMethod("BackedBy" + serializerName, serializerTypeName, stubClassName));
                }
            }
            else
            {
                var serializerTypeName = _contract.Serializers[0].AdapterClassName;
                factoryMethods.Add(GenerateFactoryMethod("", serializerTypeName, stubClassName));
            }

            return factoryMethods.ToArray();
        }

        private MethodDeclarationSyntax GenerateFactoryMethod(string suffix, TypeString serializerAdapterType, TypeString stubTypeName)
        {
            var serializerCreateClause = SF.EqualsValueClause(
                SF.ObjectCreationExpression(SH.ShortTypeName(serializerAdapterType))
                .WithArgumentList(SF.ArgumentList()));

            var serializerVarStatement = SH.VarDeclaration("serializer", serializerCreateClause);

            var clientCreateExpression = SF.ObjectCreationExpression(SH.ShortTypeName(stubTypeName))
                .WithArgumentList(SH.CallArguments(SH.IdentifierArgument("endpoint"), SH.IdentifierArgument("serializer")));

            var returnStatement = SF.ReturnStatement(clientCreateExpression);

            var endpointParam = SH.Parameter("endpoint", Names.RpcClientEndpointBaseClass.Full);

            return SF.MethodDeclaration(SF.ParseTypeName(stubTypeName.Short), "CreateInstance" + suffix)
                .AddModifiers(SF.Token(SyntaxKind.PublicKeyword), SF.Token(SyntaxKind.StaticKeyword))
                .AddParameterListParameters(endpointParam)
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
