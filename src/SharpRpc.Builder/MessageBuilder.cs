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
    public enum MessageType
    {
        OneWay,
        Request,
        Response
    }

    public class MessageBuilder
    {
        private readonly SortedList<string, PropertyDeclarationSyntax> _properties = new SortedList<string, PropertyDeclarationSyntax>();
        private readonly List<ParamDeclaration> _usedParams = new List<ParamDeclaration>();
        private ClassDeclarationSyntax _messageClassDeclaration;

        internal MessageBuilder(ContractDeclaration contract, CallDeclaration callDec, MessageType type)
        {
            ContractInfo = contract;
            RpcInfo = callDec;
            MessageType = type;
        }

        public TypeString MessageClassName { get; private set; }
        public CallDeclaration RpcInfo { get; }
        public ContractDeclaration ContractInfo { get; }
        public IReadOnlyList<ParamDeclaration> MessageParams => _usedParams;
        public MessageType MessageType { get; }

        internal static void GenerateMessages(ContractDeclaration contract, GeneratorExecutionContext context)
        {
            foreach (var call in contract.Calls)
            {
                if (call.CallType == ContractCallType.ClientCall || call.CallType == ContractCallType.ServerCall)
                {
                    new MessageBuilder(contract, call, MessageType.Request).GenerateMessage(context, true, Names.RequestClassPostfix);
                    new MessageBuilder(contract, call, MessageType.Response).GenerateMessage(context, false, Names.ResponseClassPostfix);
                }
                else
                    new MessageBuilder(contract, call, MessageType.OneWay).GenerateMessage(context, true, Names.MessageClassPostfix);
            }

            GenerateMessageBase(contract, context, out var baseMessageClassName);
            CompleteSerializerBuilding(contract, context, baseMessageClassName);
        }

        private static void CompleteSerializerBuilding(ContractDeclaration contract, GeneratorExecutionContext context, TypeString baseMessageClassName)
        {
            foreach (var builder in contract.SerializerBuilders)
            {
                var serializerClassName = new TypeString(contract.Namespace, contract.InterfaceName.Short + "_" + builder.Name + "_MessageSerializer");
                builder.GenerateSerializerCode(serializerClassName, baseMessageClassName, context);
            }
        }

        private static void GenerateMessageBase(ContractDeclaration contract, GeneratorExecutionContext context, out TypeString baseMessageClassName)
        {
            baseMessageClassName = contract.BaseMessageClassName;

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(baseMessageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxHelper.GlobalTypeName(Names.MessageInterface)));

            foreach (var serializerBuilder in contract.SerializerBuilders)
                serializerBuilder.CompleteMessageBuilding(ref messageClassDeclaration);

            var stubNamespace = SyntaxFactory
                .NamespaceDeclaration(SyntaxFactory.IdentifierName(baseMessageClassName.Namespace))
                .AddMembers(messageClassDeclaration);

            var compUnit = SyntaxFactory
                .CompilationUnit()
                .AddMembers(stubNamespace);

            var srcCode = compUnit
                .NormalizeWhitespace()
                .ToFullString();

            context.AddSource(baseMessageClassName.Full, SourceText.From(srcCode, Encoding.UTF8));
        }

        public void UpdatePropertyDeclaration(string propertyName, Func<PropertyDeclarationSyntax, PropertyDeclarationSyntax> updateFunc)
        {
            _properties[propertyName] = updateFunc(_properties[propertyName]);
        }

        public void UpdateClassDeclaration(Func<ClassDeclarationSyntax, ClassDeclarationSyntax> updateFunc)
        {
            _messageClassDeclaration = updateFunc(_messageClassDeclaration);
        }

        internal void GenerateMessage(GeneratorExecutionContext context, bool direct, string namePostfix)
        {
            var contractType = ContractInfo.InterfaceName;
            MessageClassName = ContractInfo.GetMessageClassName(RpcInfo.MethodName, namePostfix);

            var compUnit = SyntaxFactory.CompilationUnit();
            var stubNamespace = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.IdentifierName(contractType.Namespace));

            _messageClassDeclaration = SyntaxFactory.ClassDeclaration(MessageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxHelper.GlobalTypeName(ContractInfo.BaseMessageClassName)));

            if (direct)
            {
                var index = 1;

                _usedParams.AddRange(RpcInfo.Params);

                foreach (var param in _usedParams)
                    _properties.Add(param.MessagePropertyName, GenerateMessageProperty(param, index++));
            }
            else
            {
                _usedParams.Add(RpcInfo.ReturnParam);

                if (RpcInfo.ReturnParam != null)
                    _properties.Add(RpcInfo.ReturnParam.MessagePropertyName, GenerateMessageProperty(RpcInfo.ReturnParam, 0));
            }

            NotifySerializers();

            _messageClassDeclaration = _messageClassDeclaration.AddMembers(_properties.Values.ToArray());

            stubNamespace = stubNamespace.AddMembers(_messageClassDeclaration);
            compUnit = compUnit.AddMembers(stubNamespace);

            var srcCode = compUnit
                .NormalizeWhitespace()
                .ToFullString();

            context.AddSource(MessageClassName.Full, SourceText.From(srcCode, Encoding.UTF8));
        }

        private void NotifySerializers()
        {
            foreach (var builder in ContractInfo.SerializerBuilders)
                builder.BuildUpMessage(this);
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
