// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using MessagePack;
using SharpRpc;
using System;
using System.Collections.Generic;
using System.Text;

namespace TestCommon
{
    [RpcServiceContract(EnablePrebuilder = true)]
    [RpcSerializer(SerializerChoice.MessagePack)]
    public interface BenchmarkContract
    {
        [RpcContract(0, RpcType.Message)]
        void SendUpdate(FooEntity entity);

        [RpcContract(1, RpcType.Call)]
        void ApplyUpdate(FooEntity entity);

        [RpcContract(2, RpcType.Call)]
        [RpcStreamInput(typeof(FooEntity))]
        void UpstreamUpdates();

        [RpcContract(3, RpcType.Call)]
        [RpcStreamOutput(typeof(FooEntity))]
        void DownstreamUpdates();

        [RpcContract(4, RpcType.Call)]
        void Flush();

        [RpcContract(5, RpcType.Call)]
        MulticastReport MulticastUpdateToClients(int msgCount, bool usePrebuiltMessages);

        [RpcContract(6, RpcType.CallbackMessage)]
        void SendUpdateToClient(FooEntity entity);

        [RpcContract(7, RpcType.Callback)]
        void ApplyUpdateOnClient(FooEntity entity);

        [RpcContract(8, RpcType.Call)]
        PerfReport GetPerfCounters();
    }

    [MessagePackObject]
    public class MulticastReport
    {
        [Key(1)]
        public int MessageSent { get; set; }

        [Key(2)]
        public int MessageFailed { get; set; }

        [Key(3)]
        public TimeSpan Elapsed { get; set; }
    }

    [MessagePackObject]
    public class PerfReport
    {
        [Key(1)]
        public int RxMessagePageCount { get; set; }

        [Key(2)]
        public double AverageRxChunkSize { get; set; }

        [Key(3)]
        public double AverageRxMessagePageSize { get; set; }
    }

    public static class BenchmarkContractCfg
    {
        public static void ConfigureEndpoint(Endpoint endpoint)
        {
            //endpoint.RxTimeout = TimeSpan.FromSeconds(5);
            //endpoint.EnableKeepAlive(TimeSpan.FromSeconds(1));

            endpoint.RxBufferSegmentSize = short.MaxValue * 1;
            endpoint.TxBufferSegmentSize = short.MaxValue * 1;

            endpoint.AsyncMessageParsing = false;
        }

        public static int GetPort(bool secure)
        {
            return secure ? 8413 : 8412;
        }
    }
}
