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
using System.Threading.Tasks;

namespace TestCommon
{
    public class BenchmarkServiceImpl : BenchmarkContract_Gen.ServiceBase
    {
        private readonly FooMulticaster _multicaster;

        public BenchmarkServiceImpl(FooMulticaster multicaster)
        {
            _multicaster = multicaster;
        }

        public override void OnInit()
        {
            _multicaster.Add(Client);
        }

        public override void OnClose()
        {
            _multicaster.Remove(Client);
        }

#if NET5_0_OR_GREATER
        public override ValueTask SendUpdate(FooEntity entity)
#else
        public override Task SendUpdate(FooEntity entity)
#endif
        {
            return FwAdapter.AsyncVoid;
        }

#if NET5_0_OR_GREATER
        public override ValueTask ApplyUpdate(CallContext context, FooEntity entity)
#else
        public override Task ApplyUpdate(CallContext context, FooEntity entity)
#endif
        {
            return FwAdapter.AsyncVoid;
        }

#if NET5_0_OR_GREATER
        public override ValueTask Flush(CallContext context)
#else
        public override Task Flush(CallContext context)
#endif
        {
            return FwAdapter.AsyncVoid;
        }

#if NET5_0_OR_GREATER
        public override ValueTask<MulticastReport> MulticastUpdateToClients(CallContext context, int msgCount, bool usePrebuiltMessages, bool useStreams)
#else
        public override Task<MulticastReport> MulticastUpdateToClients(CallContext context, int msgCount, bool usePrebuiltMessages, bool useStreams)
#endif
        {
            if (useStreams)
                return FwAdapter.WrappResult(_multicaster.MulticastStreamItems(msgCount));
            else
                return FwAdapter.WrappResult(_multicaster.MulticastMessages(msgCount, usePrebuiltMessages));
        }

#if NET5_0_OR_GREATER
        public override ValueTask<PerfReport> GetPerfCounters(CallContext context)
#else
        public override Task<PerfReport> GetPerfCounters(CallContext context)
#endif
        {
            var rep = new PerfReport();

#if PF_COUNTERS
            rep.AverageRxBufferSize = Session.AverageRxBufferSize;
            rep.AverageRxMessagesPerBuffer = Session.AverageRxMessagesPerBuffer;
#endif
            return FwAdapter.WrappResult(rep);
        }

#if NET5_0_OR_GREATER
        public override async ValueTask UpstreamUpdates(CallContext context, StreamReader<FooEntity> inputStream)
        {
            var summ = 0.0;

            await foreach (var update in inputStream)
                summ += update.Ask;
        }

        public override async ValueTask DownstreamUpdates(CallContext context, StreamWriter<FooEntity> outputStream)
        {
            var adapter = _multicaster.Add(outputStream);
            await adapter.Completion;
        }
#else
        public override Task UpstreamUpdates(CallContext context, StreamReader<FooEntity> inputStream)
        {
            throw new NotImplementedException();
        }

        public override async Task DownstreamUpdates(CallContext context, StreamWriter<FooEntity> outputStream)
        {
            var adapter =  _multicaster.Add(outputStream);
            await adapter.Completion;
        }
#endif
    }
}
