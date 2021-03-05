using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    internal class ServiceBinding
    {
        private readonly Func<RpcServiceBase> _serivceImplFactory;

        public ServiceBinding(Func<RpcServiceBase> serivceImplFactory, IRpcSerializer serializer)
        {
            _serivceImplFactory = serivceImplFactory;
            Serializer = serializer;
        }

        public IRpcSerializer Serializer { get; }

        public RpcServiceBase CreateServiceImpl()
        {
            return _serivceImplFactory();
        }
    }
}
