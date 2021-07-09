// Copyright © 2021 Soft-Fx. All rights reserved.
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

            Session.Opened += (s, a) => _multicaster.Add(Client);
            Session.Closed += (s, a) => _multicaster.Remove(Client);
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
        public override ValueTask ApplyUpdate(FooEntity entity)
#else
        public override Task ApplyUpdate(FooEntity entity)
#endif
        {
            return FwAdapter.AsyncVoid;
        }

#if NET5_0_OR_GREATER
        public override ValueTask Flush()
#else
        public override Task Flush()
#endif
        {
            return FwAdapter.AsyncVoid;
        }

#if NET5_0_OR_GREATER
        public override ValueTask<MulticastReport> MulticastUpdateToClients(int msgCount, bool usePrebuiltMessages)
#else
        public override Task<MulticastReport> MulticastUpdateToClients(int msgCount, bool usePrebuiltMessages)
#endif
        {
            return FwAdapter.WrappResult(_multicaster.Multicast(msgCount, usePrebuiltMessages));
        }

#if NET5_0_OR_GREATER
        public override ValueTask<PerfReport> GetPerfCounters()
#else
        public override Task<PerfReport> GetPerfCounters()
#endif
        {
            var rep = new PerfReport();

#if PF_COUNTERS
            rep.AverageRxChunkSize = Session.AverageRxChunkSize;
            rep.AverageRxMessagePageSize = Session.AverageRxMessagePageSize;
            rep.RxMessagePageCount = Session.RxMessagePageCount;
#endif
            return FwAdapter.WrappResult(rep);
        }
    }
}
