using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpRpc.Builder
{
    internal class MessageBuilder
    {
        private ContractDeclaration _contract;

        public MessageBuilder(ContractDeclaration contract)
        {
            _contract = contract;
        }

        public void GenerateCode(GeneratorExecutionContext context)
        {
            foreach (var call in _contract.Calls)
            {
                if (call.CallType == ContractCallType.ClientCall || call.CallType == ContractCallType.ServerCall)
                {
                    GenerateMessage(call, context, true, Names.RequestClassPostfix);
                    GenerateMessage(call, context, false, Names.ResponceClassPostfix);
                }
                else
                    GenerateMessage(call, context, true, Names.MessageClassPostfix);
            }
        }

        private bool GenerateMessage(CallDeclaration call, GeneratorExecutionContext context, bool direct, string namePostfix)
        {
            var contractType = _contract.TypeName;
            var messageTypeName = Names.GetMessageName(contractType.Short, call.MethodName, namePostfix);
            var messageType = new TypeString(contractType.Namespace, messageTypeName);

            var compUnit = SyntaxFactory.CompilationUnit();
            var stubNamespace = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.IdentifierName(contractType.Namespace));

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(messageType.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(Names.RpcMessageInterface.Full)));

            if (direct)
            {
                var index = 1;
                var properties = call.Params
                    .Select(p => GenerateMessageProperty(p, index++))
                    .ToArray();

                messageClassDeclaration = messageClassDeclaration.AddMembers(properties);
            }
            else
            {
                if (call.ReturnParam != null)
                {
                    var returnProp = GenerateMessageProperty(call.ReturnParam, 0);
                    messageClassDeclaration = messageClassDeclaration.AddMembers(returnProp);
                }
            }

            stubNamespace = stubNamespace.AddMembers(messageClassDeclaration);
            compUnit = compUnit.AddMembers(stubNamespace);

            var srcCode = compUnit
                .NormalizeWhitespace()
                .ToFullString();

            context.AddSource(messageType.Full, SourceText.From(srcCode, Encoding.UTF8));

            return true;
        }

        private PropertyDeclarationSyntax GenerateMessageProperty(ParamDeclaration callProp, int index)
        {
            return SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(callProp.ParamType), callProp.MessagePropertyName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();
        }
    }
}
