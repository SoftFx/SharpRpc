namespace SharpRpc
{
    public struct RpcResult
    {
        internal RpcResult(RetCode code, RpcFault fault = null)
        {
            Code = code;
            Fault = fault;
        }

        internal RpcResult(RetCode code, string message = null)
        {
            Code = code;

            if (code != RetCode.Ok)
                Fault = new RpcFault(message);
            else
                Fault = null;
        }

        public static readonly RpcResult Ok = new RpcResult(RetCode.Ok, (RpcFault)null);

        public RetCode Code { get; }
        public RpcFault Fault { get; }
    }

    public struct RpcResult<T>
    {
        public RpcResult(RetCode code, T result, RpcFault fault = null)
        {
            Code = code;
            Result = result;
            Fault = fault;
        }

        public RetCode Code { get; }
        public RpcFault Fault { get; }
        public T Result { get; }
    }

    public class RpcFault
    {
        public RpcFault(string message)
        {
            Message = message;
        }

        public string Message { get; protected set; }
    }

    public enum RetCode
    {
        Ok,
        Error
    }
}