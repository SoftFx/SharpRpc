﻿using System;
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
        public static readonly TypeString RpcResultStruct = new TypeString("SharpRpc.RpcResult");
        public static readonly TypeString RpcServiceBaseClass = new TypeString("SharpRpc.RpcServiceBase");
        public static readonly TypeString RpcSerializerInterface = new TypeString("SharpRpc.IRpcSerializer");
        public static readonly TypeString MessageReaderClass = new TypeString("SharpRpc.MessageReader");
        public static readonly TypeString MessageWriterClass = new TypeString("SharpRpc.MessageWriter");

        public static readonly string MessageClassPostfix = "Message";
        public static readonly string RequestClassPostfix = "Request";
        public static readonly string ResponseClassPostfix = "Response";

        public static readonly string RpcServiceBaseOnMessageMethod = "OnMessage";
        public static readonly string RpcServiceBaseOnRequestMethod = "OnRequest";
        public static readonly string RpcServiceBaseOnUnknownMessage = "OnUnknownMessage";

        public static readonly string RpcSerializeMethod = "Serialize";
        public static readonly string RpcDeserializeMethod = "Deserialize";

        public static readonly string WriterBufferProperty = "ByteBuffer";
        public static readonly string ReaderBufferProperty = "ByteBuffer";

        public static readonly string SystemTask = "System.Threading.Tasks.Task";
        public static readonly string SystemValueTask = "System.Threading.Tasks.ValueTask";
       
    }
}