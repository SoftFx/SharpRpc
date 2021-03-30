using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.Builder
{
    public static class Names
    {
        public static readonly TypeString ContractAttributeClass = new TypeString("SharpRpc.RpcContractAttribute");
        public static readonly TypeString RpcAttributeClass = new TypeString("SharpRpc.RpcAttribute");
        public static readonly TypeString RpcSerializerAttributeClass = new TypeString("SharpRpc.RpcSerializerAttribute");

        public static readonly TypeString RpcClientBaseClass = new TypeString("SharpRpc.ClientBase");
        public static readonly TypeString RpcClientEndpointBaseClass = new TypeString("SharpRpc.ClientEndpoint");

        public static readonly TypeString MessageInterface = new TypeString("SharpRpc.IMessage");
        public static readonly TypeString RequestInterface = new TypeString("SharpRpc.IRequest");
        public static readonly TypeString ResponseInterface = new TypeString("SharpRpc.IResponse");

        public static readonly TypeString LoginMessageInterface = new TypeString("SharpRpc.ILoginMessage");
        public static readonly TypeString LogoutMessageInterface = new TypeString("SharpRpc.ILogoutMessage");
        public static readonly TypeString HeartbeatMessageInterface = new TypeString("SharpRpc.IHeartbeatMessage");
        public static readonly TypeString MessageFactoryInterface = new TypeString("SharpRpc.ISystemMessageFactory");

        public static readonly TypeString MessageReaderClass = new TypeString("SharpRpc.MessageReader");
        public static readonly TypeString MessageWriterClass = new TypeString("SharpRpc.MessageWriter");

        public static readonly TypeString RpcServerClass = new TypeString("SharpRpc.RpcServer");
        public static readonly TypeString RpcServiceBaseClass = new TypeString("SharpRpc.RpcServiceBase");

        public static readonly TypeString RpcSerializerInterface = new TypeString("SharpRpc.IRpcSerializer");
        public static readonly TypeString SerializerChoiceEnum = new TypeString("SharpRpc.SerializerChoice");
        public static readonly TypeString RpcResultStruct = new TypeString("SharpRpc.RpcResult");

        public static readonly TypeString RpcConfigurationException = new TypeString("SharpRpc.RpcConfigurationException");
        
        public static readonly string MessageClassPostfix = "Message";
        public static readonly string RequestClassPostfix = "Request";
        public static readonly string ResponseClassPostfix = "Response";
        public static readonly string ResponseResultProperty = "Result";

        public static readonly string RpcServiceBaseOnMessageMethod = "OnMessage";
        public static readonly string RpcServiceBaseOnRequestMethod = "OnRequest";
        public static readonly string RpcServiceBaseOnUnknownMessage = "OnUnknownMessage";
        public static readonly string RpcServiceBaseOnUnknownRequest = "OnUnknownRequest";

        public static readonly string RpcSerializeMethod = "Serialize";
        public static readonly string RpcDeserializeMethod = "Deserialize";

        public static readonly string WriterBufferProperty = "ByteBuffer";
        public static readonly string ReaderBufferProperty = "ByteBuffer";

        public static readonly string FacadeSerializerAdapterFactoryMethod = "CreateSerializationAdapter";
        public static readonly string ServiceCreateFaultResponseMethod = "CreateFaultResponse";

        public static readonly string SystemTask = "System.Threading.Tasks.Task";
        public static readonly string SystemValueTask = "System.Threading.Tasks.ValueTask";
        public static readonly string SystemException = "System.Exception";
    }
}