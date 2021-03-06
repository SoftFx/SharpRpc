﻿// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc;
using System;
using System.Collections.Generic;
using System.Text;

namespace Benchmark.Common
{
    [RpcContract]
    [RpcSerializer(SerializerChoice.MessagePack)]
    public interface BenchmarkContract
    {
        [Rpc(RpcType.Message)]
        void SendUpdate(FooEntity entity);

        [Rpc(RpcType.Call)]
        void SendUpdate2(FooEntity entity);

        [Rpc(RpcType.Call)]
        void DummyMethod1(int p1, string p2);

        [Rpc(RpcType.Call)]
        string DummyMethod2(int p1, string p2);
    }

    public static class BenchmarkContractCfg
    {
        public static void ConfigureEndpoint(Endpoint endpoint)
        {
            endpoint.RxTimeout = TimeSpan.FromSeconds(5);
            endpoint.EnableKeepAlive(TimeSpan.FromSeconds(1));
        }

        public static int GetPort(bool secure)
        {
            return secure ? 8413 : 8412;
        }
    }
}
