// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.Builder
{
    public static class Names
    {
        public static readonly TypeString ServiceContractAttributeClass = new TypeString("SharpRpc.RpcServiceContractAttribute");
        public static readonly TypeString RpcContractAttributeClass = new TypeString("SharpRpc.RpcContractAttribute");
        public static readonly TypeString RpcSerializerAttributeClass = new TypeString("SharpRpc.RpcSerializerAttribute");
        public static readonly TypeString RpcFaultAttributeClass = new TypeString("SharpRpc.RpcFaultAttribute");
        public static readonly TypeString RpcStreamInputAttributeClass = new TypeString("SharpRpc.RpcStreamInputAttribute");
        public static readonly TypeString RpcStreamOutputAttributeClass = new TypeString("SharpRpc.RpcStreamOutputAttribute");

        public static readonly TypeString RpcClientBaseClass = new TypeString("SharpRpc.ClientBase");
        public static readonly TypeString RpcClientFacadeBaseClass = new TypeString("SharpRpc.ClientFacadeBase");
        public static readonly TypeString RpcClientEndpointBaseClass = new TypeString("SharpRpc.ClientEndpoint");
        public static readonly TypeString RpcChannelClass = new TypeString("SharpRpc.Channel");
        public static readonly TypeString RpcSessionInfoClass = new TypeString("SharpRpc.SessionInfo");

        public static readonly TypeString ContractDescriptorClass = new TypeString("SharpRpc.ContractDescriptor");
        public static readonly TypeString ServiceBindingClass = new TypeString("SharpRpc.ServiceBinding");

        public static readonly TypeString MessageInterface = new TypeString("SharpRpc.IMessage");
        public static readonly TypeString RequestInterface = new TypeString("SharpRpc.IRequestMessage");
        public static readonly TypeString ResponseInterface = new TypeString("SharpRpc.IResponseMessage");
        public static readonly TypeString FaultMessageInterface = new TypeString("SharpRpc.IRequestFaultMessage");
        public static readonly TypeString CustomFaultBindingInterface = new TypeString("SharpRpc.ICustomFaultBinding");
        
        public static readonly TypeString StreamRequestInterface = new TypeString("SharpRpc.IOpenStreamRequest");
        public static readonly TypeString StreamPageInterface = new TypeString("SharpRpc.IStreamPage");
        public static readonly TypeString StreamFactoryInterface = new TypeString("SharpRpc.IStreamMessageFactory");
        public static readonly TypeString StreamCompletionMessageInterface = new TypeString("SharpRpc.IStreamCompletionMessage");
        public static readonly TypeString StreamPageAckInterface = new TypeString("SharpRpc.IStreamPageAck");

        public static readonly TypeString RpcInputStreamCallClass = new TypeString("SharpRpc.InputStreamCall");
        public static readonly TypeString RpcDuplexStreamCallClass = new TypeString("SharpRpc.DuplexStreamCall");
        public static readonly TypeString RpcOutputStreamCallClass = new TypeString("SharpRpc.OutputStreamCall");

        public static readonly TypeString BasicRpcFault = new TypeString("SharpRpc.RpcFault");

        public static readonly TypeString LoginMessageInterface = new TypeString("SharpRpc.ILoginMessage");
        public static readonly TypeString LoginResultEnum = new TypeString("SharpRpc.LoginResult");
        public static readonly TypeString LogoutMessageInterface = new TypeString("SharpRpc.ILogoutMessage");
        public static readonly TypeString HeartbeatMessageInterface = new TypeString("SharpRpc.IHeartbeatMessage");
        public static readonly TypeString MessageFactoryInterface = new TypeString("SharpRpc.ISystemMessageFactory");
        public static readonly TypeString RpcPrebuiltMessage = new TypeString("SharpRpc.PrebuiltMessage");
        public static readonly TypeString RpcMultiPrebuiltMessage = new TypeString("SharpRpc.MultiPrebuiltMessage");

        public static readonly TypeString MessageReaderClass = new TypeString("SharpRpc.MessageReader");
        public static readonly TypeString MessageWriterClass = new TypeString("SharpRpc.MessageWriter");

        public static readonly TypeString RpcServerClass = new TypeString("SharpRpc.RpcServer");
        public static readonly TypeString RpcCallHandlerClass = new TypeString("SharpRpc.RpcCallHandler");

        public static readonly TypeString RpcSerializerInterface = new TypeString("SharpRpc.IRpcSerializer");
        public static readonly TypeString RpcPreserializeTool = new TypeString("SharpRpc.PreserializeTool");
        public static readonly TypeString SerializerChoiceEnum = new TypeString("SharpRpc.SerializerChoice");
        public static readonly TypeString RpcResultStruct = new TypeString("SharpRpc.RpcResult");

        public static readonly TypeString RpcSegmentedByteArray = new TypeString("SharpRpc.SegmentedByteArray");

        public static readonly TypeString RpcConfigurationException = new TypeString("SharpRpc.RpcConfigurationException");
        public static readonly TypeString RpcFaultException = new TypeString("SharpRpc.RpcFaultException");

        public static readonly string ResponseResultProperty = "Result";

        public static readonly string PrebuildCallOption = "EnablePrebuilder";

        public static readonly string RpcServiceBaseOnInitMethod = "OnInit";
        public static readonly string RpcServiceBaseOnMessageMethod = "OnMessage";
        public static readonly string RpcServiceBaseOnRequestMethod = "OnRequest";
        public static readonly string RpcServiceBaseOnUnknownMessage = "OnUnknownMessage";
        public static readonly string RpcServiceBaseOnUnknownRequest = "OnUnknownRequest";

        public static readonly string RpcFaultAttributeTypesProperty = "FaultTypes";

        public static readonly string RpcSerializeMethod = "Serialize";
        public static readonly string RpcDeserializeMethod = "Deserialize";

        public static readonly string WriterBufferProperty = "ByteBuffer";
        public static readonly string ReaderBufferProperty = "ByteBuffer";
        public static readonly string WriterStreamProperty = "ByteStream";
        public static readonly string ReaderStreamProperty = "ByteStream";

        public static readonly string FacadeSerializerAdapterFactoryMethod = "CreateSerializationAdapter";
        public static readonly string FacadeCreateDescriptorMethod = "CreateDescriptor";
        public static readonly string ServiceOnRegularFaultMethod = "OnRegularFault";
        public static readonly string ServiceOnUnexpectedFaultMethod = "OnUnexpectedFault";
        public static readonly string ServiceOnCustomFaultMethod = "OnCustomFault";

        public static readonly string SystemTask = "System.Threading.Tasks.Task";
        public static readonly string SystemValueTask = "System.Threading.Tasks.ValueTask";
        public static readonly string SystemException = "System.Exception";

        public static TypeSyntax GetReaderStreamStubType(string streamSubTye)
        {
            return SyntaxHelper.GenericType("SharpRpc.StreamReader", streamSubTye);
        }

        public static TypeSyntax GetWriterStreamStubType(string streamSubTye)
        {
            return SyntaxHelper.GenericType("SharpRpc.StreamWriter", streamSubTye);
        }
    }
}