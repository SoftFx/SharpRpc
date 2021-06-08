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
using System.Dynamic;

namespace TestClient
{
    internal class Benchmark
    {
        public static void LaunchTestSeries(string address, int multiplier)
        {
            DoTest(address, 500000 * multiplier, 1, TestOptions.OneWay);
            DoTest(address, 500000 * multiplier, 1, TestOptions.OneWay | TestOptions.Async);
            DoTest(address, 500000 * multiplier, 1, TestOptions.OneWay | TestOptions.Prebuild);
            DoTest(address, 500000 * multiplier, 1, TestOptions.OneWay | TestOptions.Async | TestOptions.Prebuild);

            //DoTest(address, 500000 * multiplier, 1, TestOptions.OneWay | TestOptions.SSL);
            //DoTest(address, 500000 * multiplier, 1, TestOptions.OneWay | TestOptions.Async | TestOptions.SSL);

            //DoTest(5000000, 1, true, true, ConcurrencyMode.PagedQueue, true);

            DoTest(address, 1000 * multiplier, 1, TestOptions.None);
            DoTest(address, 1000 * multiplier, 1, TestOptions.Async);
            //DoTest(address, 1000 * multiplier, 1, TestOptions.SSL);
            //DoTest(address, 1000 * multiplier, 1, TestOptions.Async | TestOptions.SSL);
        }

        private static void DoTest(string serverAddress, int msgCount, int clientCount, TestOptions options) //, bool oneWay, bool async, bool enableSsl)
        {
            var nameBuilder = new StringBuilder();
            nameBuilder.Append("Test: ");
            nameBuilder.Append("X").Append(clientCount);

            var oneWay = options.HasFlag(TestOptions.OneWay);
            var async = options.HasFlag(TestOptions.Async);
            var enableSsl = options.HasFlag(TestOptions.SSL);
            var prebuild = options.HasFlag(TestOptions.Prebuild);

            if (oneWay)
                nameBuilder.Append(" | OneWay");
            if (async)
                nameBuilder.Append(" | Async");
            if (enableSsl)
                nameBuilder.Append(" | SSL");
            if (prebuild)
                nameBuilder.Append(" | Prebuild");

            Console.WriteLine(nameBuilder.ToString());

            var gens = Enumerable
                .Range(0, clientCount)
                .Select(i => new EntitySet<FooEntity>(EntityGenerator.GenerateRandomEntities()))
                .ToList();

            var prebuilder = new BenchmarkContract_Gen.Prebuilder();

            var prebuildGens = Enumerable
                .Range(0, clientCount)
                .Select(i => new EntitySet<BenchmarkContract_Gen.PrebuiltMessages.SendUpdate>(
                    EntityGenerator.GenerateRandomEntities().Select(e => prebuilder.PrebuildSendUpdate(e))))
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
                    var sendLoops = new Task[clientCount];

                    for (int i = 0; i < clientCount; i++)
                    {
                        var client = clients[i];

                        if (oneWay)
                            sendLoops[i] = SendMessages(client, msgCount, gens[i], prebuildGens[i], async, prebuild);
                        else
                            sendLoops[i] = DoCalls(client, msgCount, gens[i], async);
                    }

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

            var toWait = 5;

            Console.WriteLine();
            Console.WriteLine("Pause " + toWait + "  sec...");
            Console.WriteLine();

            GC.Collect(2, GCCollectionMode.Forced, true, true);

            Task.Delay(TimeSpan.FromSeconds(toWait)).Wait();
        }

        private static Task SendMessages(BenchmarkContract_Gen.Client client, int msgCount, EntitySet<FooEntity> set,
            EntitySet<BenchmarkContract_Gen.PrebuiltMessages.SendUpdate> prebultSet, bool isAsync, bool usePrebuilt)
        {
            if (isAsync)
                return SendMsgAsyncLoop(client, msgCount, set, prebultSet, usePrebuilt);
            else
                return Task.Factory.StartNew(() => SendMessageLoop(client, msgCount, set, prebultSet, usePrebuilt));
        }

        private static Task DoCalls(BenchmarkContract_Gen.Client client, int msgCount, EntitySet<FooEntity> set, bool isAsync)
        {
            if (isAsync)
                return AsyncCallLoop(client, msgCount, set);
            else
                return Task.Factory.StartNew(() => SyncCallLoop(client, msgCount, set));
        }

        private static void SendMessageLoop(BenchmarkContract_Gen.Client client, int msgCount, EntitySet<FooEntity> set,
            EntitySet<BenchmarkContract_Gen.PrebuiltMessages.SendUpdate> prebultSet, bool userPrebuild)
        {
            if (userPrebuild)
            {
                for (int i = 0; i < msgCount; i++)
                    client.SendUpdate(prebultSet.Next());
            }
            else
            {
                for (int i = 0; i < msgCount; i++)
                    client.SendUpdate(set.Next());
            }
        }

        private static async Task SendMsgAsyncLoop(BenchmarkContract_Gen.Client client, int msgCount, EntitySet<FooEntity> set,
            EntitySet<BenchmarkContract_Gen.PrebuiltMessages.SendUpdate> prebultSet, bool usePrebuild)
        {
            await Task.Yield();

            if (usePrebuild)
            {
                for (int i = 0; i < msgCount; i++)
                    await client.SendUpdateAsync(prebultSet.Next());
            }
            else
            {
                for (int i = 0; i < msgCount; i++)
                    await client.SendUpdateAsync(set.Next());
            }   
        }

        private static async Task AsyncCallLoop(BenchmarkContract_Gen.Client client, int msgCount, EntitySet<FooEntity> set)
        {
            for (int i = 0; i < msgCount; i++)
            {
                var msg = set.Next();
                await client.ApplyUpdateAsync(msg);
            }
        }

        private static void SyncCallLoop(BenchmarkContract_Gen.Client client, int msgCount, EntitySet<FooEntity> set)
        {
            for (int i = 0; i < msgCount; i++)
            {
                var msg = set.Next();
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

        [Flags]
        private enum TestOptions
        {
            None = 0,
            OneWay = 1,
            Async = 2,
            SSL = 4,
            Prebuild = 8
        }
    }
}
