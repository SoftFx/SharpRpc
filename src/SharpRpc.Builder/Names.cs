using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.Builder
{
    public static class Names
    {
        public static readonly TypeString ContractAttributeClass = new TypeString("SharpRpc.RpcContractAttribute");
        public static readonly TypeString RpcAttributeClass = new TypeString("SharpRpc.RpcAttribute");
        public static readonly TypeString RpcClientBaseClass = new TypeString("SharpRpc.ClientBase");
        public static readonly TypeString RpcClientEndpointBaseClass = new TypeString("SharpRpc.ClientEndpoint");
        public static readonly TypeString RpcMessageInterface = new TypeString("SharpRpc.IMessage");
        public static readonly TypeString RpcResultStruct = new TypeString("SharpRpc.RpcResult");

        public static readonly string MessageClassPostfix = "Message";
        public static readonly string RequestClassPostfix = "Request";
        public static readonly string ResponceClassPostfix = "Response";

        public static readonly string SystemTask = "System.Threading.Tasks.Task";
        public static readonly string SystemValueTask = "System.Threading.Tasks.ValueTask";

        public static string GetOnWayMessageName(string contractName, string contractMethodName)
        {
            return GetMessageName(contractName, contractMethodName, MessageClassPostfix);
        }

        public static string GetRequestName(string contractName, string contractMethodName)
        {
            return GetMessageName(contractName, contractMethodName, RequestClassPostfix);
        }

        public static string GetMessageName(string contractName, string contractMethodName, string postfix)
        {
            return contractName + "_" + contractMethodName + postfix;
        }
    }
}