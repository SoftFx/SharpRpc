// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestCommon
{
    [RpcServiceContract(EnablePrebuilder = true)]
    [RpcSerializer(SerializerChoice.MessagePack)]
    public interface SyntaxTestContract
    {
        [RpcContract(1, RpcType.Call)]
        [RpcStreamOutput(typeof(int))]
        int OutStreamCall(TimeSpan delay, int count, StreamTestOptions options);

        [RpcContract(2, RpcType.Call)]
        [RpcStreamOutput(typeof(int))]
        void OutStreamCallNoRet(TimeSpan delay, int count, StreamTestOptions options);

        [RpcContract(3, RpcType.Call)]
        [RpcStreamInput(typeof(int))]
        int InStreamCall(TimeSpan delay, StreamTestOptions options);

        [RpcContract(4, RpcType.Call)]
        [RpcStreamInput(typeof(int))]
        void InStreamCallNoRet(TimeSpan delay, StreamTestOptions options);

        [RpcContract(5, RpcType.Call)]
        [RpcStreamInput(typeof(int))]
        [RpcStreamOutput(typeof(long))]
        int DuplexStreamCall(TimeSpan delay, StreamTestOptions options);

        [RpcContract(6, RpcType.Call)]
        [RpcStreamInput(typeof(int))]
        [RpcStreamOutput(typeof(long))]
        void tDuplexStreamCallNoRet(TimeSpan delay, StreamTestOptions options);
    }
}
