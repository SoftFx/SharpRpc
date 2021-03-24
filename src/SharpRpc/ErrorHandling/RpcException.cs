using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    public class RpcException : Exception
    {
        public RpcException(string message, RpcRetCode errorCode)
            : base(message)
        {
            ErrorCode = errorCode;
        }

        public RpcRetCode ErrorCode { get; }

        public RpcResult ToRpcResult()
        {
            return new RpcResult(ErrorCode, new RpcFault(Message));
        }
    }

    public class RpcFaultException<T> : RpcException
        where T : RpcFault
    {
        public RpcFaultException(RpcRetCode errorCode, string message)
            : base(message, errorCode)
        {
        }

        public T Fault { get; }
    }

    public class RpcConfigurationException : RpcException
    {
        public RpcConfigurationException(string message)
            : base(message, RpcRetCode.ConfigurationError)
        {
        }
    }
}
