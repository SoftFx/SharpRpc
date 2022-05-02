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
using System.Threading;
using System.Threading.Tasks;

namespace TestCommon.StressTest
{
    public class StressTestService : StressTestContract_Gen.ServiceBase
    {
        private readonly Random _rndSeed = new Random();

#if NET5_0_OR_GREATER
        public override async ValueTask DownstreamEntities(CallContext context, StreamWriter<StressEntity> outputStream, RequestConfig cfg, int count)
#else
        public override async Task DownstreamEntities(CallContext context, StreamWriter<StressEntity> outputStream, RequestConfig cfg, int count)
#endif
        {
            ThrowIfRequested(cfg);

            var generator = new StressEntityGenerator();

            for (int i = 0; i < count; i++)
            {
                var wResult = await outputStream.WriteAsync(generator.Next());

                if (!wResult.IsOk)
                    throw new RpcFaultException("Streaming faulted: " + wResult.Code);
            }
        }

#if NET5_0_OR_GREATER
        public override async ValueTask RequestMessages(CallContext context, int count, RequestConfig cfg)
#else
        public override async Task RequestMessages(CallContext context, int count, RequestConfig cfg)
#endif
        {
            ThrowIfRequested(cfg);

            var generator = new StressEntityGenerator();

            for (int i = 0; i < count; i++)
                await Client.Async.CallbackMessage(cfg.Id, generator.Next());
        }

#if NET5_0_OR_GREATER
        public override ValueTask<StressEntity> RequestResponse(CallContext context, StressEntity entity, RequestConfig cfg)
#else
        public override Task<StressEntity> RequestResponse(CallContext context, StressEntity entity, RequestConfig cfg)
#endif
        {
            ThrowIfRequested(cfg);

            return FwAdapter.WrappResult(entity);
        }

#if NET5_0_OR_GREATER
        public override async ValueTask<int> UpstreamEntities(CallContext context, StreamReader<StressEntity> inputStream, RequestConfig cfg)
#else
        public override async Task<int> UpstreamEntities(CallContext context, StreamReader<StressEntity> inputStream, RequestConfig cfg)
#endif
        {
            ThrowIfRequested(cfg);

            var count = 0;

            var cancelSrc = new CancellationTokenSource();

            if (cfg.CancelAfterMs > 0)
                cancelSrc.CancelAfter(cfg.CancelAfterMs);

            var rnd = new Random(_rndSeed.Next());

            if (cfg.HasItemPause)
                await RandomItemDelay(cfg, rnd);

#if NET5_0_OR_GREATER
            await foreach (var entity in inputStream.WithCancellation(cancelSrc.Token))
#else
            var e = inputStream.GetEnumerator(cancelSrc.Token);

            while (await e.MoveNextAsync())
#endif
            {
                count++;

                if (cfg.HasItemPause)
                    await RandomItemDelay(cfg, rnd);
            }

            if (cfg.HasItemPause)
                await RandomItemDelay(cfg, rnd);

            return count;
        }

#if NET5_0_OR_GREATER
        public override async ValueTask DuplexStreamEntities(CallContext context, StreamReader<StressEntity> inputStream, StreamWriter<StressEntity> outputStream, RequestConfig cfg)
#else
        public override async Task DuplexStreamEntities(CallContext context, StreamReader<StressEntity> inputStream, StreamWriter<StressEntity> outputStream, RequestConfig cfg)
#endif
        {
            ThrowIfRequested(cfg);

            var rxE = inputStream.GetEnumerator();

            while (await rxE.MoveNextAsync())
            {
                var sendResult = await outputStream.WriteAsync(rxE.Current);
                if (!sendResult.IsOk)
                    break;
            }

            await outputStream.CompleteAsync();
        }

        private void ThrowIfRequested(RequestConfig cfg)
        {
            if (!string.IsNullOrEmpty(cfg.Fault))
                throw new RpcFaultException(cfg.Fault);
        }

        private Task RandomItemDelay(RequestConfig cfg, Random rnd)
        {
            var randomPauseMs = rnd.Next(0, cfg.PerItemPauseMs);
            return Task.Delay(randomPauseMs);
        }
    }
}
