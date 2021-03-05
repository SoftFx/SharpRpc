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
            BaseMessageClassName = new TypeString(InterfaceName.Namespace, InterfaceName.Short + "_RpcMessageBase");
        }

        public TypeString InterfaceName { get; }
        public string Namespace => InterfaceName.Namespace;
        public TypeString BaseMessageClassName { get; }
        public List<CallDeclaration> Calls { get; } = new List<CallDeclaration>();

        internal IReadOnlyList<SerializerDeclaration> Serializers => _serializers;

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
            return new TypeString(InterfaceName.Namespace, InterfaceName.Short + "_" + contractMethodName + postfix);
        }
    }
}
