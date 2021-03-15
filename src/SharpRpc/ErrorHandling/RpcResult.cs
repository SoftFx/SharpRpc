namespace SharpRpc
{
    public struct RpcResult
    {
        public static RpcResult<T1> CreateFromResult<T1>(T1 result)
        {
            return new RpcResult<T1>(result);
        }

        internal RpcResult(RpcRetCode code, RpcFault fault)
        {
            Code = code;
            Fault = fault ?? new RpcFault("");
        }

        internal RpcResult(RpcRetCode code, string message)
        {
            Code = code;
            Fault = new RpcFault(message ?? "");
        }

        public static readonly RpcResult Ok = new RpcResult();
        public static readonly RpcResult ChannelClose = new RpcResult(RpcRetCode.ChannelClosed, "Operation has been aborted due to Channel close.");

        public RpcRetCode Code { get; }
        public RpcFault Fault { get; }

        public void ThrowIfNotOk()
        {
            if (Code != RpcRetCode.Ok)
                throw new RpcException(Fault.Message, Code);
        }
    }

    public struct RpcResult<T>
    {
        public RpcResult(T result)
        {
            Code = RpcRetCode.Ok;
            Result = result;
            Fault = null;
        }

        public RpcResult(RpcRetCode code, RpcFault fault)
        {
            Code = code;
            Result = default(T);
            Fault = fault ?? new RpcFault("");
        }

        public RpcResult(RpcRetCode code, string message)
        {
            Code = code;
            Result = default(T);
            Fault = new RpcFault(message ?? "");
        }

        public RpcRetCode Code { get; }
        public RpcFault Fault { get; }
        public T Result { get; }

        public RpcResult GetResultInfo()
        {
            return new RpcResult(Code, Fault);
        }

        public void ThrowIfNotOk()
        {
            if (Code != RpcRetCode.Ok)
                throw new RpcException(Fault.Message, Code);
        }
    }

    public class RpcFault
    {
        public RpcFault(string message)
        {
            Message = message;
        }

        public string Message { get; protected set; }
    }
}