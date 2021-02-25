using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    public class ServiceBinding
    {
        private readonly Func<RpcServiceBase> _serivceImplFactory;

        public ServiceBinding(Func<RpcServiceBase> serivceImplFactory, SerializerChoice serializer)
        {
            _serivceImplFactory = serivceImplFactory;
            Serializer = serializer;
        }

        public SerializerChoice Serializer { get; }

        public RpcServiceBase CreateServiceImpl()
        {
            return _serivceImplFactory();
        }
    }
}
