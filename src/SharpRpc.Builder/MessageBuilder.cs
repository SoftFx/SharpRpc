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
        Response,
        System
    }

    public class MessageBuilder
    {
        private readonly List<PropertyDeclarationSyntax> _properties = new List<PropertyDeclarationSyntax>();
        //private readonly List<ParameterSyntax> _msgProps = new List<ParameterSyntax>();
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
        public IReadOnlyList<PropertyDeclarationSyntax> MessageProperties => _properties;
        public MessageType MessageType { get; }

        internal static IEnumerable<ClassDeclarationSyntax> GenerateSystemMessages(ContractDeclaration contract)
        {
            yield return new MessageBuilder(contract, null, MessageType.System).GenerateLoginMessage();
            yield return new MessageBuilder(contract, null, MessageType.System).GenerateLogoutMessage();
            yield return new MessageBuilder(contract, null, MessageType.System).GenerateHeartbeatMessage();
        }

        internal static IEnumerable<ClassDeclarationSyntax> GenerateMessages(ContractDeclaration contract, GeneratorExecutionContext context)
        {
            foreach (var call in contract.Calls)
            {
                if (call.CallType == ContractCallType.ClientCall || call.CallType == ContractCallType.ServerCall)
                {
                    yield return new MessageBuilder(contract, call, MessageType.Request).GenerateMessage(context, true, Names.RequestClassPostfix);
                    yield return new MessageBuilder(contract, call, MessageType.Response).GenerateMessage(context, false, Names.ResponseClassPostfix);
                }
                else
                    yield return new MessageBuilder(contract, call, MessageType.OneWay).GenerateMessage(context, true, Names.MessageClassPostfix);
            }

            yield return GenerateMessageBase(contract, context);
        }

        internal static IEnumerable<ClassDeclarationSyntax> GenerateSerializationAdapters(ContractDeclaration contract, GeneratorExecutionContext context)
        {
            foreach (var serializerDec in contract.Serializers)
            {
                yield return serializerDec.Builder.GenerateSerializerAdapter(serializerDec.AdapterClassName, contract.BaseMessageClassName, context);
            }
        }

        internal static ClassDeclarationSyntax GenerateFactory(ContractDeclaration contractInfo)
        {
            var factoryInterface = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.MessageFactoryInterface));

            var loginMsgMethod = GenerateFactoryMethod("CreateLoginMessage", Names.LoginMessageInterface, contractInfo.LoginMessageClassName);
            var logoutMsgMethod = GenerateFactoryMethod("CreateLogoutMessage", Names.LogoutMessageInterface, contractInfo.LogoutMessageClassName);
            var heartbeatMsgMethod = GenerateFactoryMethod("CreateHeartBeatMessage", Names.HeartbeatMessageInterface, contractInfo.HeartbeatMessageClassName);

            return SyntaxFactory.ClassDeclaration(contractInfo.MessageFactoryClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword))
                .AddBaseListTypes(factoryInterface)
                .AddMembers(loginMsgMethod, logoutMsgMethod, heartbeatMsgMethod);
        }

        private static MethodDeclarationSyntax GenerateFactoryMethod(string methodName, TypeString retType, TypeString messageType)
        {
            var retStatement = SyntaxFactory.ReturnStatement(
                SyntaxFactory.ObjectCreationExpression(SyntaxHelper.FullTypeName(messageType))
                    .WithoutArguments());

            return SyntaxFactory.MethodDeclaration(SyntaxHelper.FullTypeName(retType), methodName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBodyStatements(retStatement);
        }

        private static ClassDeclarationSyntax GenerateMessageBase(ContractDeclaration contract, GeneratorExecutionContext context)
        {
            var baseMessageClassName = contract.BaseMessageClassName;

            var messageClassDeclaration = SyntaxFactory.ClassDeclaration(baseMessageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.AbstractKeyword))
                .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxHelper.GlobalTypeName(Names.MessageInterface)));

            foreach (var serializerEtnry in contract.Serializers)
                serializerEtnry.Builder.CompleteMessageBuilding(ref messageClassDeclaration);

            return messageClassDeclaration;
        }

        private ClassDeclarationSyntax GenerateLoginMessage()
        {
            MessageClassName = ContractInfo.LoginMessageClassName;

            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(ContractInfo.BaseMessageClassName));
            var iLoginBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.LoginMessageInterface));

            _messageClassDeclaration = SyntaxFactory.ClassDeclaration(MessageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iLoginBase);

            //_properties.Add(

            NotifySerializers();

            return _messageClassDeclaration.AddMembers(_properties.ToArray());
        }

        private ClassDeclarationSyntax GenerateLogoutMessage()
        {
            MessageClassName = ContractInfo.LogoutMessageClassName;

            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(ContractInfo.BaseMessageClassName));
            var iLogoutBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.LogoutMessageInterface));

            _messageClassDeclaration = SyntaxFactory.ClassDeclaration(MessageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iLogoutBase);

            //_properties.Add(

            NotifySerializers();

            return _messageClassDeclaration.AddMembers(_properties.ToArray());
        }

        private ClassDeclarationSyntax GenerateHeartbeatMessage()
        {
            MessageClassName = ContractInfo.HeartbeatMessageClassName;

            var msgBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(ContractInfo.BaseMessageClassName));
            var iHeartbeatBase = SyntaxFactory.SimpleBaseType(SyntaxHelper.FullTypeName(Names.HeartbeatMessageInterface));

            _messageClassDeclaration = SyntaxFactory.ClassDeclaration(MessageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(msgBase, iHeartbeatBase);

            //_properties.Add(

            NotifySerializers();

            return _messageClassDeclaration.AddMembers(_properties.ToArray());
        }

        public void UpdatePropertyDeclaration(int index, Func<PropertyDeclarationSyntax, PropertyDeclarationSyntax> updateFunc)
        {
            _properties[index] = updateFunc(_properties[index]);
        }

        public void UpdateClassDeclaration(Func<ClassDeclarationSyntax, ClassDeclarationSyntax> updateFunc)
        {
            _messageClassDeclaration = updateFunc(_messageClassDeclaration);
        }

        internal ClassDeclarationSyntax GenerateMessage(GeneratorExecutionContext context, bool direct, string namePostfix)
        {
            MessageClassName = ContractInfo.GetMessageClassName(RpcInfo.MethodName, namePostfix);

            var baseTypes = new List<BaseTypeSyntax>();
            baseTypes.Add(SyntaxFactory.SimpleBaseType(SyntaxHelper.ShortTypeName(ContractInfo.BaseMessageClassName)));

            if (MessageType == MessageType.Request)
                baseTypes.Add(SyntaxFactory.SimpleBaseType(SyntaxHelper.GlobalTypeName(Names.RequestInterface)));
            else if (MessageType == MessageType.Response)
            {
                if (RpcInfo.ReturnsData)
                    baseTypes.Add(SyntaxFactory.SimpleBaseType(SyntaxHelper.GenericType(Names.RequestInterface.Full, RpcInfo.ReturnParam.ParamType)));
                else
                    baseTypes.Add(SyntaxFactory.SimpleBaseType(SyntaxHelper.GlobalTypeName(Names.ResponseInterface)));
            }

            _messageClassDeclaration = SyntaxFactory.ClassDeclaration(MessageClassName.Short)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddBaseListTypes(baseTypes.ToArray());

            if (MessageType == MessageType.Request || MessageType == MessageType.Response)
            {
                _properties.Add(GenerateMessageProperty("string", "CallId"));
            }

            if (direct)
            {
                var index = 1;

                foreach (var param in RpcInfo.Params)
                    _properties.Add(GenerateMessageProperty(param, index++));
            }
            else
            {
                if (RpcInfo.ReturnsData)
                    _properties.Add(GenerateMessageProperty(RpcInfo.ReturnParam.ParamType, Names.ResponseResultProperty));
            }

            NotifySerializers();

            return _messageClassDeclaration.AddMembers(_properties.ToArray());
        }

        private void NotifySerializers()
        {
            foreach (var serializerEntry in ContractInfo.Serializers)
                serializerEntry.Builder.BuildUpMessage(this);
        }

        private PropertyDeclarationSyntax GenerateMessageProperty(string type, string name)
        {
            return SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(type), name)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAutoGetter()
                .AddAutoSetter();
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
