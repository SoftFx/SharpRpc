using Benchmark.Common;
using SharpRpc;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Benchmark.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            //Task.Delay(TimeSpan.FromSeconds(5)).Wait();

            DoTest(5000000, 1, true, false, ConcurrencyMode.PagedQueueX1);
            DoTest(5000000, 1, true, true, ConcurrencyMode.PagedQueueX1);

            DoTest(100000, 1, false, false, ConcurrencyMode.PagedQueueX1);
            DoTest(100000, 1, false, true, ConcurrencyMode.PagedQueueX1);

            Console.Read();
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
                .Select(i => BenchmarkContract_Gen.CreateClient(new TcpClientEndpoint("localhost", GetPort(concurrency))))
                .ToList();

            var connects = clients
                .Select(c => c.Channel.TryConnectAsync().AsTask())
                .ToArray();

            Task.WaitAll(connects);

            Exception ex = null;

            var execTime = MeasureTime(() =>
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
                await client.TrySendUpdate2Async(msg);
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

        private static int GetPort(ConcurrencyMode mode)
        {
            switch (mode)
            {
                case ConcurrencyMode.NoQueue: return 812;
                //case ConcurrencyMode.DataflowX1: return 813;
                case ConcurrencyMode.PagedQueueX1: return 814;
                default: throw new InvalidOperationException();
            }
        }
    }
}
