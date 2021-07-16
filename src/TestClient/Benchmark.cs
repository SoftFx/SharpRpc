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
using System.IO;
using ProtoBuf.Serializers;

namespace TestClient
{
    internal class Benchmark
    {
        private readonly string _address;

        public List<TestCase> Cases { get; } = new List<TestCase>();

        public Benchmark(string serverAddress)
        {
            _address = serverAddress;
        }

        public void LaunchTestSeries(int multiplier)
        {
            //LaunchTestSeriesOndeSide(_address, multiplier, TestOptions.None);
            LaunchTestSeriesOndeSide(_address, multiplier, TestOptions.Backwards);
        }

        private void LaunchTestSeriesOndeSide(string address, int multiplier, TestOptions options)
        {
            // one way

            //DoOneWayTestSeries(address, 500000, 1,  multiplier, options);
            DoOneWayTestSeries(address, 50000, 10, multiplier, options);
            DoOneWayTestSeries(address, 20000, 30, multiplier, options);
            //DoOneWayTestSeries(address, 10000, 50, multiplier, options);

            // one way (SSL)

            //DoOneWayTestSeries(address, 500000, 1, multiplier, options | TestOptions.SSL);
            //DoOneWayTestSeries(address, 50000, 10, multiplier, options | TestOptions.SSL);
            //DoOneWayTestSeries(address, 10000, 50, multiplier, options | TestOptions.SSL);

            // request-response

            //DoTest(address, 1000 * multiplier, 1, TestOptions.None);
            //DoTest(address, 1000 * multiplier, 1, TestOptions.Async);

            // request-response (SSL)

            //DoTest(address, 1000 * multiplier, 1, TestOptions.SSL);
            //DoTest(address, 1000 * multiplier, 1, TestOptions.Async | TestOptions.SSL);
        }

        private void DoOneWayTestSeries(string address, int msgCount, int clientCount, int multiplier, TestOptions baseOptions)
        {
            //DoTest(address, msgCount * multiplier, clientCount, baseOptions | TestOptions.OneWay);
            DoTest(address, msgCount * multiplier, clientCount, baseOptions | TestOptions.OneWay | TestOptions.Async);
            //DoTest(address, msgCount * multiplier, clientCount, baseOptions | TestOptions.OneWay | TestOptions.Prebuild);
            DoTest(address, msgCount * multiplier, clientCount, baseOptions | TestOptions.OneWay | TestOptions.Async | TestOptions.Prebuild);
        }

        public void PrintReportToConsole()
        {
            Console.WriteLine(FormatTextReport());
        }

        public void SaveReportToFile(string path = null)
        {
            if (path == null)
            {
                var repDir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory)))));
                path = Path.Combine(repDir, "Benchmark results.txt");
            }

            File.AppendAllText(path, FormatTextReport());
        }

        private string FormatTextReport()
        {
            var repBuilder = new StringBuilder();

            repBuilder.AppendLine("---------------------------");
            repBuilder.AppendLine("Benchmark Report " + DateTime.Now);
            repBuilder.AppendLine("---------------------------");
            repBuilder.AppendLine();
            repBuilder.AppendLine("Framework: " + AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName);
            repBuilder.AppendLine("Version: " + typeof(Channel).Assembly.GetName().Version);
#if DEBUG
            repBuilder.AppendLine("Build: Debug");
#else
            repBuilder.AppendLine("Build: Release");
#endif
            repBuilder.AppendLine("Debugger attached: " + Debugger.IsAttached);
            repBuilder.AppendLine("Server: " + _address);
            repBuilder.AppendLine();

            var tblFormat = "{0,6} {1,5} {2,5} {3,6} {4,5} {5,6} {6,10} {7,12}";

            repBuilder.AppendFormat(tblFormat, " Side ", "  X  ", " SSL ", "Async", " 1Way ", "PreBlt", "Elapsed", "MsgPerSec");
            repBuilder.AppendLine();
            repBuilder.AppendLine();

            foreach (var testCase in Cases)
            {
                var side = testCase.Backwards ? "Server" : "Client";
                var msgPreSec = testCase.Failed ? "FAILED" : testCase.MessagePerSecond.ToString("n0");
                var elapsed = testCase.Elapsed.TotalSeconds.ToString("n1") + "s";

                repBuilder.AppendFormat(tblFormat, side, "X"+testCase.ClientCount, ToCheckSymbol(testCase.Ssl),
                    ToCheckSymbol(testCase.Async), ToCheckSymbol(testCase.OneWay), ToCheckSymbol(testCase.Prebuilt),
                    elapsed, msgPreSec);
                repBuilder.AppendLine();
            }

            repBuilder.AppendLine();

            return repBuilder.ToString();
        }

        private void DoTest(string serverAddress, int msgCount, int clientCount, TestOptions options)
        {
            DoTest(serverAddress, new TestCase(msgCount, clientCount, options), true);
        }

        private void DoTest(string serverAddress, TestCase testCase, bool preconect)
        {
            var nameBuilder = new StringBuilder();
            nameBuilder.Append("Test: ");
            nameBuilder.Append("X").Append(testCase.ClientCount);
            nameBuilder.Append(" ").Append(testCase.Backwards ? "ToClient" : "ToServer");

            if (testCase.OneWay)
                nameBuilder.Append(" | OneWay");
            if (testCase.Async)
                nameBuilder.Append(" | Async");
            if (testCase.Ssl)
                nameBuilder.Append(" | SSL");
            if (testCase.Prebuilt)
                nameBuilder.Append(" | Prebuild");

            Console.WriteLine(nameBuilder.ToString());

            var gens = Enumerable
                .Range(0, testCase.ClientCount)
                .Select(i => EntityGenerator.GenerateRandomSet())
                .ToList();

            var prebuildGens = Enumerable
                .Range(0, testCase.ClientCount)
                .Select(i => EntityGenerator.GenerateRandomPrebuiltSet())
                .ToList();

            var clients = Enumerable
                .Range(0, testCase.ClientCount)
                .Select(i => CreateClient(serverAddress, testCase.Ssl))
                .ToList();

            if (preconect)
            {
                var connectTasks = clients
                    .Select(c => c.Channel.TryConnectAsync().ToTask())
                    .ToArray();

                Task.WaitAll(connectTasks);

                if (!connectTasks.All(c => c.Result.IsOk))
                    testCase.Ex = connectTasks.First(c => !c.Result.IsOk).Result.ToException();
            }

            if (testCase.Ex == null)
            {
                if (testCase.Backwards)
                    ServerToClientTest(clients, testCase);
                else
                    ClientToServerTest(clients, testCase);
            }

#if PF_COUNTERS
            var rep = CollectPerfCounters(clients, testCase.Backwards);
#endif

            var closeTasks = clients
                .Select(c => c.Channel.CloseAsync())
                .ToArray();

            Task.WaitAll(closeTasks);

            if (testCase.Ex == null)
            {
                Console.WriteLine();

                var totalMsgCount = testCase.MessageCount * testCase.ClientCount;
                var execTime = testCase.Elapsed;
                testCase.MessagePerSecond = totalMsgCount / execTime.TotalSeconds;

                Console.WriteLine("\telapsed: {0:f1} sec", execTime.TotalSeconds);
                Console.WriteLine("\tbandwidth: {0:f0} ", testCase.MessagePerSecond);

#if PF_COUNTERS
                Console.WriteLine("\ttot. rx page count: {0:f0}", rep.RxMessagePageCount);
                Console.WriteLine("\tavg. rx page size: {0:f0}", rep.AverageRxMessagePageSize);
                Console.WriteLine("\tavg. rx chunk size: {0:f0}", rep.AverageRxChunkSize);
#endif
            }
            else
            {
                Console.WriteLine("Test failed: " + testCase.Ex.Message);
            }

            Cases.Add(testCase);

            var toWait = 5;

            Console.WriteLine();
            Console.WriteLine("Pause " + toWait + "  sec...");
            Console.WriteLine();

            GC.Collect(2, GCCollectionMode.Forced, true, true);

            Task.Delay(TimeSpan.FromSeconds(toWait)).Wait();
        }

        private static void ClientToServerTest(List<BenchmarkClient> clients, TestCase testCase)
        {
            var gens = Enumerable
                .Range(0, testCase.ClientCount)
                .Select(i => EntityGenerator.GenerateRandomSet())
                .ToList();

            var prebuildGens = Enumerable
                .Range(0, testCase.ClientCount)
                .Select(i => EntityGenerator.GenerateRandomPrebuiltSet())
                .ToList();

            testCase.Elapsed = MeasureTime(() =>
            {
                try
                {
                    var sendLoops = new Task[testCase.ClientCount];

                    for (int i = 0; i < testCase.ClientCount; i++)
                    {
                        var client = clients[i];

                        if (testCase.OneWay)
                            sendLoops[i] = SendMessages(client.Stub, testCase.MessageCount, gens[i], prebuildGens[i], testCase.Async, testCase.Prebuilt);
                        else
                            sendLoops[i] = DoCalls(client.Stub, testCase.MessageCount, gens[i], testCase.Async);
                    }

                    Task.WaitAll(sendLoops);
                }
                catch (AggregateException aex)
                {
                    testCase.Ex = aex.InnerException;
                }
            });
        }

        private static void ServerToClientTest(List<BenchmarkClient> clients, TestCase testCase)
        {
            testCase.Elapsed = MeasureTime(() =>
            {
                try
                {
                    var rep = clients[0].Stub.MulticastUpdateToClients(testCase.MessageCount, testCase.Prebuilt);
                    testCase.MessageFailedCount = rep.MessageFailed;
                    //Console.WriteLine("ELAPSED ON SERVER " + rep.Elapsed);
                }
                catch (AggregateException aex)
                {
                    testCase.Ex = aex.InnerException;
                }
            });
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

            client.Flush();
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

#if PF_COUNTERS
        private static PerfReport CollectPerfCounters(List<BenchmarkClient> clients, bool queryServer)
        {
            var result = new PerfReport();

            if (queryServer)
            {
                result.RxMessagePageCount = clients.Sum(c => c.Channel.GetRxMessagePageCount());
                result.AverageRxChunkSize = clients.Average(c => c.Channel.GetAverageRxChunkSize());
                result.AverageRxMessagePageSize = clients.Average(c => c.Channel.GetAverageRxMessagePageSize());
            }
            else
            {
                var tasks = clients.Select(c => c.Stub.GetPerfCountersAsync()).ToArray();
                Task.WaitAll(tasks);
                var reps = tasks.Select(t => t.Result).ToArray();

                result.RxMessagePageCount = reps.Sum(r => r.RxMessagePageCount);
                result.AverageRxChunkSize = reps.Average(r => r.AverageRxChunkSize);
                result.AverageRxMessagePageSize = reps.Average(r => r.AverageRxMessagePageSize);
            }

            return result;
        }
#endif

        private static TimeSpan MeasureTime(Action a)
        {
            var watch = Stopwatch.StartNew();
            a();
            watch.Stop();
            return watch.Elapsed;
        }

        private static BenchmarkClient CreateClient(string address, bool secure)
        {
            return new BenchmarkClient(address, BenchmarkContractCfg.GetPort(secure), GetSecurity(secure));
        }

        private static TcpSecurity GetSecurity(bool secure)
        {
            if (secure)
                return new SslSecurity((s, cert, chain, errs) => true);
            else
                return TcpSecurity.None;
        }

        private static string ToCheckSymbol(bool val)
        {
            if (val)
                return "  ✓  ";
            else
                return "";
        }

        [Flags]
        public enum TestOptions
        {
            None = 0,
            OneWay = 1,
            Async = 2,
            SSL = 4,
            Prebuild = 8,
            Backwards = 16,
        }

        public class TestCase
        {
            public TestCase(int msgCount, int clientCount, TestOptions options)
            {
                MessageCount = msgCount;
                ClientCount = clientCount;

                OneWay = options.HasFlag(TestOptions.OneWay);
                Async = options.HasFlag(TestOptions.Async);
                Ssl = options.HasFlag(TestOptions.SSL);
                Prebuilt = options.HasFlag(TestOptions.Prebuild);
                Backwards = options.HasFlag(TestOptions.Backwards);
            }

            public int MessageCount { get; }
            public int ClientCount { get; }

            public bool OneWay { get; }
            public bool Async { get; }
            public bool Ssl { get; }
            public bool Prebuilt { get; }
            public bool Backwards { get; }

            // Results

            public bool Failed => Ex != null || MessageFailedCount > 0;

            public int MessageFailedCount { get; set; }
            public TimeSpan Elapsed { get; set; }
            public double MessagePerSecond { get; set; }
            public Exception Ex { get; set; }
        }
    }
}
