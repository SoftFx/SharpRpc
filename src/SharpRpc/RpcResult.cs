using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public struct RpcResult
    {
        internal RpcResult(RetCode code, string message = null)
        {
            Code = code;
            Message = message;
        }

        public static readonly RpcResult Ok = new RpcResult(RetCode.Ok);

        public RetCode Code { get; }
        public string Message { get; }
    }

    public struct RetStatus<T>
    {
        public RetStatus(RetCode code, T result, string message = null)
        {
            Code = code;
            Result = result;
            Message = message;
        }

        public RetCode Code { get; }
        public string Message { get; }
        public T Result { get; }
    }

    public enum RetCode
    {
        Ok,
        Error
    }
}
