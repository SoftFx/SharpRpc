// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using TestCommon;
using SharpRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace TestClient
{
    internal class Benchmark
    {
        public static void LaunchTestSeries(string address, int multiplier)
        {
            DoTest(address, 500000 * multiplier, 1, true, false, false);
            DoTest(address, 500000 * multiplier, 1, true, true, false);
            DoTest(address, 500000 * multiplier, 1, true, false, true);
            DoTest(address, 500000 * multiplier, 1, true, true, true);

            //DoTest(5000000, 1, true, true, ConcurrencyMode.PagedQueue, true);

            DoTest(address, 1000 * multiplier, 1, false, false, false);
            DoTest(address, 1000 * multiplier, 1, false, true, false);
            DoTest(address, 1000 * multiplier, 1, false, false, true);
            DoTest(address, 1000 * multiplier, 1, false, true, true);
        }

        private static void DoTest(string serverAddress, int msgCount, int clientCount, bool oneWay, bool async, bool enableSsl)
        {
            var nameBuilder = new StringBuilder();
            nameBuilder.Append("Test: ");
            nameBuilder.Append("X").Append(clientCount);
            if (oneWay)
                nameBuilder.Append(" | OneWay");
            if (async)
                nameBuilder.Append(" | Async");
            if (enableSsl)
                nameBuilder.Append(" | SSL");

            Console.WriteLine(nameBuilder.ToString());

            var gens = Enumerable
                .Range(0, clientCount)
                .Select(i => new EntityGenerator())
                .ToList();

            var clients = Enumerable
                .Range(0, clientCount)
                .Select(i => CreateClient(serverAddress, enableSsl))
                .ToList();

            var connects = clients
                .Select(c => c.Channel.TryConnectAsync().AsTask())
                .ToArray();

            Task.WaitAll(connects);

            Exception ex = null;
            TimeSpan execTime = default;

            //if (connects.All(c => c.Result.IsOk))
            //{

            execTime = MeasureTime(() =>
            {
                try
                {
                    var sendLoops = clients
                            .Zip(gens, (c, g) => oneWay ? SendMessages(c, msgCount, g, async) : DoCalls(c, msgCount, g, async))
                            .ToArray();

                    Task.WaitAll(sendLoops);
                }
                catch (AggregateException aex)
                {
                    ex = aex.InnerException;
                }
            });

            //}
            //else
            //{
            //    ex = connects.First(c => !c.Result.IsOk).Result.ToException();
            //}

            var closeTasks = clients
                .Select(c => c.Channel.CloseAsync())
                .ToArray();

            Task.WaitAll(closeTasks);

            if (ex == null)
            {
                Console.WriteLine();

                var totalMsgCount = msgCount * clientCount;

                var perSec = (double)totalMsgCount / execTime.TotalSeconds;

                Console.WriteLine("\telapsed: {0:f1} sec", execTime.TotalSeconds);
                Console.WriteLine("\tbandwidth: {0:f0} ", perSec);
            }
            else
            {
                Console.WriteLine("Test failed: " + ex.Message);
            }

            Console.WriteLine();
            Console.WriteLine("Pause 10 sec...");
            Console.WriteLine();

            GC.Collect(2, GCCollectionMode.Forced, true, true);

            Task.Delay(TimeSpan.FromSeconds(10));
        }

        private static Task SendMessages(BenchmarkContract_Gen.Client client, int msgCount, EntityGenerator generator, bool isAsync)
        {
            if (isAsync)
                return SendMsgAsyncLoop(client, msgCount, generator);
            else
                return Task.Factory.StartNew(() => SendMessageLoop(client, msgCount, generator));
        }

        private static Task DoCalls(BenchmarkContract_Gen.Client client, int msgCount, EntityGenerator generator, bool isAsync)
        {
            if (isAsync)
                return AsyncCallLoop(client, msgCount, generator);
            else
                return Task.Factory.StartNew(() => SyncCallLoop(client, msgCount, generator));
        }

        private static void SendMessageLoop(BenchmarkContract_Gen.Client client, int msgCount, EntityGenerator generator)
        {
            for (int i = 0; i < msgCount; i++)
            {
                var msg = generator.Next();
                client.SendUpdate(msg);
            }
        }

        private static async Task SendMsgAsyncLoop(BenchmarkContract_Gen.Client client, int msgCount, EntityGenerator generator)
        {
            await Task.Yield();

            for (int i = 0; i < msgCount; i++)
            {
                var msg = generator.Next();
                await client.SendUpdateAsync(msg);
            }
        }

        private static async Task AsyncCallLoop(BenchmarkContract_Gen.Client client, int msgCount, EntityGenerator generator)
        {
            for (int i = 0; i < msgCount; i++)
            {
                var msg = generator.Next();
                await client.ApplyUpdateAsync(msg);
            }
        }

        private static void SyncCallLoop(BenchmarkContract_Gen.Client client, int msgCount, EntityGenerator generator)
        {
            for (int i = 0; i < msgCount; i++)
            {
                var msg = generator.Next();
                client.ApplyUpdate(msg);
            }
        }

        private static TimeSpan MeasureTime(Action a)
        {
            var watch = Stopwatch.StartNew();
            a();
            watch.Stop();
            return watch.Elapsed;
        }

        private static BenchmarkContract_Gen.Client CreateClient(string address, bool secure)
        {
            var security = GetSecurity(secure);
            var endpoint = new TcpClientEndpoint(address, BenchmarkContractCfg.GetPort(secure), security);
            endpoint.Credentials = new BasicCredentials("Admin", "zzzz");

            return BenchmarkContract_Gen.CreateClient(endpoint);
        }

        private static TcpSecurity GetSecurity(bool secure)
        {
            if (secure)
                return new SslSecurity((s, cert, chain, errs) => true);
            else
                return TcpSecurity.None;
        }
    }
}
