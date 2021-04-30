// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

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
            if (code != RpcRetCode.Ok)
                Fault = fault ?? new RpcFault("");
            else
                Fault = null;
        }

        internal RpcResult(RpcRetCode code, string message)
        {
            Code = code;
            if (code != RpcRetCode.Ok)
                Fault = new RpcFault(message ?? "");
            else
                Fault = null;
        }

        public static readonly RpcResult Ok = new RpcResult();
        public static readonly RpcResult ChannelClose = new RpcResult(RpcRetCode.ChannelClosed, "Operation has been aborted due to Channel close.");

        public RpcRetCode Code { get; }
        public RpcFault Fault { get; }
        public bool IsOk => Code == RpcRetCode.Ok;

        public void ThrowIfNotOk()
        {
            if (Code != RpcRetCode.Ok)
                throw new RpcException(Fault.Message, Code);
        }

        public RpcException ToException()
        {
            return new RpcException(Fault.Message, Code);
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