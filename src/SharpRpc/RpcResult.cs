namespace SharpRpc
{
    public struct RpcResult
    {
        internal RpcResult(RetCode code, RpcFault fault = null)
        {
            Code = code;
            Fault = fault;
        }

        public static readonly RpcResult Ok = new RpcResult(RetCode.Ok);

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