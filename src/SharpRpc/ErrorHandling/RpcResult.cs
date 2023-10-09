// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Threading.Tasks;

namespace SharpRpc
{
    public struct RpcResult
    {
        public static RpcResult<T1> Result<T1>(T1 result)
        {
            return new RpcResult<T1>(result);
        }

#if NET5_0_OR_GREATER
        public static ValueTask<RpcResult<T>> AsyncResult<T>(T result)
        {
            return new ValueTask<RpcResult<T>>(Result<T>(result));
        }

        public static ValueTask<RpcResult<T>> AsyncFault<T>(RpcResult fault)
        {
            return new ValueTask<RpcResult<T>>(Fault<T>(fault));
        }
#else
        public static Task<RpcResult<T>> AsyncResult<T>(T result)
        {
            return Task.FromResult(Result<T>(result));
        }

        public static Task<RpcResult<T>> AsyncFault<T>(RpcResult fault)
        {
            return Task.FromResult(Fault<T>(fault));
        }
#endif

        public static RpcResult<T2> Fault<T2>(RpcResult fault)
        {
            return new RpcResult<T2>(fault.Code, fault.FaultMessage);
        }

        public static RpcResult<T2> Fault<T2>(RpcRetCode code, string faultText)
        {
            return new RpcResult<T2>(code, faultText);
        }

        internal RpcResult(RpcRetCode code, string faultText, object customFaultData = null)
        {
            Code = code;
            FaultMessage = faultText;
            CustomFaultData = customFaultData;    
        }

        public static readonly RpcResult Ok = new RpcResult();
        internal static readonly RpcResult ChannelClose = new RpcResult(RpcRetCode.ChannelClosed, "The operation has been aborted due to Channel close.");
        internal static readonly RpcResult OperationCanceled = new RpcResult(RpcRetCode.OperationCanceled, "The operation has been canceled.");

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

        internal static RpcResult UnexpectedMessage(Type msgType, Type receiverType)
        {
            return new RpcResult(RpcRetCode.ProtocolViolation,
                $"A received message of type '{msgType.Name} is not expected/supported by communication object '{receiverType.Name}'!");
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
        public bool IsOk => Code == RpcRetCode.Ok;

        public RpcResult GetResultInfo()
        {
            return new RpcResult(Code, FaultMessage, CustomFaultData);
        }

        public T GetValueOrThrow()
        {
            if (Code != RpcRetCode.Ok)
                throw new RpcException(FaultMessage, Code);

            return Value;
        }

        //public void ThrowIfNotOk()
        //{
        //    if (Code != RpcRetCode.Ok)
        //        throw new RpcException(FaultMessage, Code);
        //}

        public static implicit operator RpcResult<T>(RpcResult r) => new RpcResult<T>(r.Code, r.FaultMessage, r.CustomFaultData);
        public static implicit operator RpcResult(RpcResult<T> r) => new RpcResult(r.Code, r.FaultMessage, r.CustomFaultData);
    }
}