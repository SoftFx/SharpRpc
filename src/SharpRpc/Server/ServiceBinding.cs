using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    public class ServiceBinding
    {
        private readonly Func<RpcServiceBase> _serivceImplFactory;

        public ServiceBinding(Func<RpcServiceBase> serivceImplFactory, IRpcSerializer serializer, ISystemMessageFactory msgFactory)
        {
            _serivceImplFactory = serivceImplFactory;
            Descriptor = new ContractDescriptor(serializer, msgFactory);
        }

        internal ContractDescriptor Descriptor { get; }

        internal RpcServiceBase CreateServiceImpl()
        {
            return _serivceImplFactory();
        }
    }
}
