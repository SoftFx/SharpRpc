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
            if (faultCode == RequestFaultCode.CustomFault)
                return RpcRetCode.RequestFaulted;
            else if (faultCode == RequestFaultCode.RegularFault)
                return RpcRetCode.RequestFaulted;
            else
                return RpcRetCode.RequestCrashed;
        }
    }
}
