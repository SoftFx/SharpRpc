// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TestCommon;

namespace TestClient
{
    internal class LoadTest
    {
        private readonly int _threadsCount;
        private readonly string _address;
        private List<Task> _threads;
        private CancellationTokenSource _stopSrc;

        // for debug puprpose
        private StressTestContract_Gen.Client[] _clients;

        public LoadTest(string address, int threads)
        {
            _address = address;
            _threadsCount = 1; // threads;
        }

        public void Start()
        {
            _stopSrc = new CancellationTokenSource();
            _clients = new StressTestContract_Gen.Client[_threadsCount];
            _threads = Enumerable.Range(0, _threadsCount)
                .Select(MessageLoadLoop)
                .ToList();
        }

        private async Task MessageLoadLoop(int index)
        {
            try
            {
                var client = CreateClient(index);
                var payload = CreatePayload(500);

                _clients[index] = client;

                while (true)
                {
                    foreach (var entity in payload)
                    {
                        if (_stopSrc.IsCancellationRequested)
                            break;

                        //await Task.Delay(1000);

                        await client.Async.LoadMessage(Guid.NewGuid(), entity, true);

                        Console.WriteLine($"[{index}] - message sent");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        private async Task UpstreamLoadLoop(int index)
        {
            var client = CreateClient(index);
            var payload = CreatePayload(100);

            _clients[index] = client;

            var streamCall = client.UpstreamEntities(new StreamOptions { }, new RequestConfig { PerItemPauseMs = 1 });

            streamCall.InputStream.EnableCancellation(_stopSrc.Token);

            while (true)
            {
                foreach (var entity in payload)
                {
                    if (_stopSrc.IsCancellationRequested)
                        break;

                    var writeResult = await streamCall.InputStream.WriteAsync(entity);

                    if (!writeResult.IsOk)
                        return;

                    Console.WriteLine($"[{index}] - message sent");
                }
            }
        }

        private StressTestContract_Gen.Client CreateClient(int index)
        {
            var endpoint = new TcpClientEndpoint(_address, 813, TcpSecurity.None);
            var callbackHandler = new CallbackHandler(index);
            return StressTestContract_Gen.CreateClient(endpoint, callbackHandler);
        }

        private IEnumerable<StressEntity> CreatePayload(int size)
        {
            //for (int i = 0; i < size; i++)
            {
                var entity = new StressEntity
                {
                    EntityProperty = new SomeOtherEntity { StrProperty = "1111111111111111111" },
                    StrArrayProperty = new List<string>(),
                    StrProperty = "1111111111111111111111111111111111"
                };

                for (var j = 0; j < 40000000; j++)
                    entity.StrArrayProperty.Add("5555555555555555555555555555555555555555555555555555555");

                yield return entity;
            }
        }

        public void Stop()
        {
            _stopSrc.Cancel();
            Task.WhenAll(_threads);
        }

        private class CallbackHandler : StressTestContract_Gen.CallbackServiceBase
        {
            private readonly int _index;

            public CallbackHandler(int index)
            {
                _index = index;
            }

#if NET5_0_OR_GREATER
            public override ValueTask CallbackMessage(Guid requestId, StressEntity entity)
#else
            public override Task CallbackMessage(Guid requestId, StressEntity entity)
#endif
            {
                Console.WriteLine($"[{_index}] - message received");

                return FwAdapter.AsyncVoid;
            }
        }
    }
}
