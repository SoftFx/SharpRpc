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

            //DoTest(10000000, 1, false);
            //DoTest(10000000, 2, false);

            //            DoTest(1000000, 4, false);
            //DoTest(1000000, 4, true);

            //DoTest(1000000, 1, false);
            //DoTest(5000000, 1, false);
            //DoTest(10000000, 1, false);
            //DoTest(20000000, 1, false);
            //DoTest(50000000, 1, false);
            //DoTest(5000000, 1, false);
            //DoTest(1000000, 1, false);

            Console.Read();
        }

        private static void DoTest(int msgPerClient, int clientCount, bool async)
        {
            Console.WriteLine("Started test size={0}, clients={1}, isAsync={2}", msgPerClient, clientCount, async);

            var gens = Enumerable
                .Range(0, clientCount)
                .Select(i => new EntityGenerator())
                .ToList();

            var clients = Enumerable
                .Range(0, clientCount)
                .Select(i => new BenchmarkClient(new TcpClientEndpoint("localhost", 812)))
                .ToList();

            var connects = clients
                .Select(c => c.Connect())
                .ToArray();

            Task.WaitAll(connects);

            var execTime = MeasureTime(() =>
            {
                var sendLoops = clients
                    .Zip(gens, (c, g) => SendMessages(c, msgPerClient, g, async))
                    .ToArray();

                Task.WaitAll(sendLoops);
            });

            Console.WriteLine("Done!");

            var totalMsgCount = msgPerClient * clientCount;

            var perSec = (double)totalMsgCount / execTime.TotalSeconds;

            Console.WriteLine("\telapsed: {0:f1} sec", execTime.TotalSeconds);
            Console.WriteLine("\tbandwidth: {0:f0} ", perSec);

            Console.WriteLine();
            Console.WriteLine("Pause 10 sec...");
            Console.WriteLine();

            GC.Collect(2, GCCollectionMode.Forced, true, true);

            Task.Delay(TimeSpan.FromSeconds(10));
        }

        private static Task SendMessages(BenchmarkClient client, int msgCount, EntityGenerator generator, bool isAsync)
        {
            if (isAsync)
                return SendMsgAsyncLoop(client, msgCount, generator);
            else
                return Task.Factory.StartNew(() => SendMessageLoop(client, msgCount, generator));
        }

        private static void SendMessageLoop(BenchmarkClient client, int msgCount, EntityGenerator generator)
        {
            for (int i = 0; i < msgCount; i++)
            {
                var msg = generator.Next();
                client.OnUpdate(msg);
            }
        }

        private static async Task SendMsgAsyncLoop(BenchmarkClient client, int msgCount, EntityGenerator generator)
        {
            await Task.Yield();

            for (int i = 0; i < msgCount; i++)
            {
                var msg = generator.Next();
                await client.OnUpdateAsync(msg);
            }
        }

        private static TimeSpan MeasureTime(Action a)
        {
            var watch = Stopwatch.StartNew();
            a();
            watch.Stop();
            return watch.Elapsed;
        }
    }
}
