// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

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
            return new RpcResult(ErrorCode, new RpcFaultStub(Message));
        }
    }

    public class RpcFaultException : RpcException
    {
        public RpcFaultException(string message)
            : base(message, RpcRetCode.RequestFaulted)
        {
        }

        public RpcFaultException(RequestFaultCode code, string text)
            : base(RpcFaultStub.GetFaultMessage(code, text), code.ToRetCode())
        {

        }

        public static RpcFaultException<T> Create<T>(T fault)
            where T : RpcFault
        {
            return new RpcFaultException<T>(fault);
        }
    }

    public class RpcFaultException<T> : RpcFaultException
        where T : RpcFault
    {
        public RpcFaultException(T fault)
            : base(fault?.Message)
        {
            Fault = fault;
        }

        public T Fault { get; }

        public static RpcFaultException<T> Create(T fault) => new(fault);
    }

    public class RpcConfigurationException : RpcException
    {
        public RpcConfigurationException(string message)
            : base(message, RpcRetCode.ConfigurationError)
        {
        }
    }
}
