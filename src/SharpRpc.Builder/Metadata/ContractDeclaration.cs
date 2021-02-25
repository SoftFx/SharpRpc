using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpRpc.Builder
{
    public class ContractDeclaration
    {
        public ContractDeclaration(string typeFullName)
        {
            InterfaceName = new TypeString(typeFullName);
        }

        public TypeString InterfaceName { get; }
        public List<SerializerBuilderBase> SerializerBuilders { get; } = new List<SerializerBuilderBase>();
        public List<CallDeclaration> Calls { get; } = new List<CallDeclaration>();
    }
}
