using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class ContractDescriptor
    {
        public ContractDescriptor(IRpcSerializer serializer, ISystemMessageFactory factory)
        {
            SerializationAdapter = serializer;
            SystemMessages = factory;
        }

        public IRpcSerializer SerializationAdapter { get; }
        public ISystemMessageFactory SystemMessages { get; }
    }
}
