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

        public RpcException(string message, RpcRetCode errorCode, Exception innerEx)
            : base(message, innerEx)
        {
            ErrorCode = errorCode;
        }

        public RpcRetCode ErrorCode { get; }

        public RpcResult ToRpcResult()
        {
            return new RpcResult(ErrorCode, Message);
        }
    }

    public class RpcFaultException : RpcException
    {
        public RpcFaultException(string message)
            : base(message, RpcRetCode.RequestFault)
        {
        }

        internal RpcFaultException(RpcRetCode code, string text)
            : base(text, code)
        {
        }

        public static RpcFaultException<T> Create<T>(T fault)
        {
            return new RpcFaultException<T>(null, fault);
        }

        public static RpcFaultException<T> Create<T>(string text, T fault)
        {
            return new RpcFaultException<T>(text, fault);
        }
    }

    public class RpcFaultException<T> : RpcFaultException
    {
        public RpcFaultException(string text, T fault)
            : base(text)
        {
            Fault = fault;
        }

        public T Fault { get; }

        public static RpcFaultException<T> Create(string text, T faultData) => new RpcFaultException<T>(text, faultData);
    }

    public class RpcConfigurationException : RpcException
    {
        public RpcConfigurationException(string message)
            : base(message, RpcRetCode.ConfigurationError)
        {
        }
    }
}
