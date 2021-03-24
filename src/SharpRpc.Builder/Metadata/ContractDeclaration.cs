using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpRpc.Builder
{
    public class ContractDeclaration
    {
        private List<SerializerDeclaration> _serializers = new List<SerializerDeclaration>();

        public ContractDeclaration(string typeFullName)
        {
            InterfaceName = new TypeString(typeFullName);
            FacadeClassName = new TypeString(InterfaceName.Namespace, InterfaceName.Short + "_Gen");
            MessageBundleClassName = new TypeString(FacadeClassName.Full, "Messages");
            BaseMessageClassName = new TypeString(MessageBundleClassName.Full, "MessageBase");
            ClientStubClassName = new TypeString(FacadeClassName.Full, "Client");
            ServiceStubClassName = new TypeString(FacadeClassName.Full, "Service");
        }

        public TypeString InterfaceName { get; }
        public TypeString FacadeClassName { get; }
        public TypeString MessageBundleClassName { get; }
        public TypeString ClientStubClassName { get; }
        public TypeString ServiceStubClassName { get; }
        public string Namespace => InterfaceName.Namespace;
        public TypeString BaseMessageClassName { get; }
        public List<CallDeclaration> Calls { get; } = new List<CallDeclaration>();

        internal IReadOnlyList<SerializerDeclaration> Serializers => _serializers;

        internal string GetDefaultSerializerChoice()
        {
            if (Serializers.Count > 0)
                return Serializers[0].Builder.EnumVal;
            else
                return "DataContract";
        }

        internal void AddSerializer(SerializerBuilderBase serializerBuilder)
        {
            _serializers.Add(new SerializerDeclaration(serializerBuilder, InterfaceName));
        }

        public TypeString GetOnWayMessageClassName(string contractMethodName)
        {
            return GetMessageClassName(contractMethodName, Names.MessageClassPostfix);
        }

        public TypeString GetRequestClassName(string contractMethodName)
        {
            return GetMessageClassName(contractMethodName, Names.RequestClassPostfix);
        }

        public TypeString GetResponseClassName(string contractMethodName)
        {
            return GetMessageClassName(contractMethodName, Names.ResponseClassPostfix);
        }

        public TypeString GetMessageClassName(string contractMethodName, string postfix)
        {
            return new TypeString(MessageBundleClassName.Short, contractMethodName + postfix);
        }
    }
}
