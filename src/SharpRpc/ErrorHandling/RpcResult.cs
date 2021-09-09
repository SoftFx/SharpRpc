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
        public static RpcResult<T1> FromResult<T1>(T1 result)
        {
            return new RpcResult<T1>(result);
        }

        internal RpcResult(RpcRetCode code, string faultText, object customFaultData = null)
        {
            Code = code;
            FaultMessage = faultText;
            CustomFaultData = customFaultData;    
        }

        public static readonly RpcResult Ok = new RpcResult();
        public static readonly RpcResult ChannelClose = new RpcResult(RpcRetCode.ChannelClosed, "Operation has been aborted due to Channel close.");

        public RpcRetCode Code { get; }
        public string FaultMessage { get; }
        public object CustomFaultData { get; }
        public bool IsOk => Code == RpcRetCode.Ok;

        public void ThrowIfNotOk()
        {
            if (Code != RpcRetCode.Ok)
                throw new RpcException(FaultMessage, Code);
        }

        public RpcException ToException()
        {
            return new RpcException(FaultMessage, Code);
        }

        internal RpcResult<T> ToValueResult<T>(T val = default(T))
        {
            if (IsOk)
                return new RpcResult<T>(val);
            else
                return new RpcResult<T>(Code, FaultMessage, CustomFaultData);
        }
    }

    public struct RpcResult<T>
    {
        public RpcResult(T result)
        {
            Code = RpcRetCode.Ok;
            Value = result;
            FaultMessage = null;
            CustomFaultData = null;
        }

        public RpcResult(RpcRetCode code, string faultText, object customFaultData = null)
        {
            Code = code;
            Value = default(T);
            FaultMessage = faultText;
            CustomFaultData = customFaultData;
        }

        public RpcRetCode Code { get; }
        public string FaultMessage { get; }
        public object CustomFaultData { get; }
        public T Value { get; }

        public RpcResult GetResultInfo()
        {
            return new RpcResult(Code, FaultMessage, CustomFaultData);
        }

        public void ThrowIfNotOk()
        {
            if (Code != RpcRetCode.Ok)
                throw new RpcException(FaultMessage, Code);
        }
    }
}