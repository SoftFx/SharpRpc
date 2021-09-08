// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal static class HelperExt
    {
        public static RpcRetCode ToRetCode(this RequestFaultCode faultCode)
        {
            if (faultCode == RequestFaultCode.Fault)
                return RpcRetCode.RequestFault;
            else
                return RpcRetCode.RequestCrash;
        }

        public static RpcResult ToRpcResult(this IRequestFaultMessage msg)
        {
            return new RpcResult(msg.Code.ToRetCode(), msg.Text, msg.GetCustomFaultData());
        }

        public static RpcResult<T> ToRpcResult<T>(this IRequestFaultMessage msg)
        {
            return new RpcResult<T>(msg.Code.ToRetCode(), msg.Text, msg.GetCustomFaultData());
        }

        public static object GetCustomFaultData(this IRequestFaultMessage faultMessage)
        {
            return faultMessage.GetCustomFaultBinding()?.GetFault();
        }

        public static RpcException CreateException(this IRequestFaultMessage faultMessage)
        {
            var binding = faultMessage.GetCustomFaultBinding();
            var text = faultMessage.Text;

            if (binding != null)
                return binding.CreateException(text);
            else
                return new RpcException(faultMessage.Text, faultMessage.Code.ToRetCode());
        }
    }
}
