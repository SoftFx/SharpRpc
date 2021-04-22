using Benchmark.Common;
using SharpRpc;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benchmark.Client
{
    internal class Benchmark
    {
        public static void LaunchTestSeries()
        {
            DoTest(5000000, 1, true, false, ConcurrencyMode.PagedQueue);
            DoTest(5000000, 1, true, true, ConcurrencyMode.PagedQueue);

            DoTest(100000, 1, false, false, ConcurrencyMode.PagedQueue);
            DoTest(100000, 1, false, true, ConcurrencyMode.PagedQueue);
        }

        private static void DoTest(int msgCount, int clientCount, bool oneWay, bool async, ConcurrencyMode concurrency)
        {
            Console.WriteLine("Started test size={0}, oneWay={4} clients={1}, isAsync={2} concurrency={3}", msgCount, clientCount, async, concurrency, oneWay);

            var gens = Enumerable
                .Range(0, clientCount)
                .Select(i => new EntityGenerator())
                .ToList();

            var clients = Enumerable
                .Range(0, clientCount)
                .Select(i => CreateClient(concurrency))
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
                Console.WriteLine("Done!");

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
                await client.SendUpdate2Async(msg);
            }
        }

        private static void SyncCallLoop(BenchmarkContract_Gen.Client client, int msgCount, EntityGenerator generator)
        {
            for (int i = 0; i < msgCount; i++)
            {
                var msg = generator.Next();
                client.SendUpdate2(msg);
            }
        }

        private static TimeSpan MeasureTime(Action a)
        {
            var watch = Stopwatch.StartNew();
            a();
            watch.Stop();
            return watch.Elapsed;
        }

        private static BenchmarkContract_Gen.Client CreateClient(ConcurrencyMode concurrency)
        {
            var endpoint = new TcpClientEndpoint("localhost", GetPort(concurrency));
            endpoint.Credentials = new BasicCredentials("Admin", "zzzz");

            return BenchmarkContract_Gen.CreateClient(endpoint);
        }

        private static int GetPort(ConcurrencyMode mode)
        {
            switch (mode)
            {
                case ConcurrencyMode.NoQueue: return 812;
                //case ConcurrencyMode.DataflowX1: return 813;
                case ConcurrencyMode.PagedQueue: return 814;
                default: throw new InvalidOperationException();
            }
        }
    }
}
