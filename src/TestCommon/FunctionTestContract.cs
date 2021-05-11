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
using SharpRpc;

namespace TestCommon
{
    [RpcContract]
    [RpcSerializer(SerializerChoice.MessagePack)]
    interface FunctionTestContract
    {
        [Rpc(RpcType.Message)]
        void TestNotify1(int p1, string p2);

        [Rpc(RpcType.Call)]
        void TestCall1(int p1, string p2);

        [Rpc(RpcType.Call)]
        string TestCall2(int p1, string p2);

        [Rpc(RpcType.Call)]
        string TestCall3(int p1, string p2);
    }
}
