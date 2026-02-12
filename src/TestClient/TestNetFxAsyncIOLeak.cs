// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace TestClient
{
    /* TP-7416
    * On .Net Framework Async IO uses pinned buffers which for some reason are not released after socket is closed.
    * All data is held by System.Threading.OverlappedData including buffers, sockets, Socket+Int32TaskSocketAsyncEventArgs.
    * Checking for leaks involves taking VS heap snapshot
    */
    internal static class TestNetFxAsyncIOLeak
    {
        private static int _totalConnects;

        public static void Run(string address)
        {
            var ssl = true;

            // run once to init statics and reduce heap diff.
            var warmupCnt = 25;
            for (var i = 0; i < warmupCnt; i++)
                ClientWorker(address, ssl, "warmup").Wait();

            for (var i = 0; i < 3; i++)
                GC.Collect(2, GCCollectionMode.Forced, true, true);

            Console.WriteLine();
            Console.WriteLine($"Warmup completed. Total connects: {_totalConnects}");
            Console.WriteLine("GC.Collect() completed. Take 1st heap shapshot and press Enter...");
            Console.ReadLine();

            var connectCnt = 100;
            for (var i = 0; i < connectCnt; i++)
                ClientWorker(address, ssl, (i + 1).ToString()).Wait();

            for (var i = 0; i < 3; i++)
                GC.Collect(2, GCCollectionMode.Forced, true, true);

            Console.WriteLine();
            Console.WriteLine($"Single thread run completed. Total connects: {_totalConnects}");
            Console.WriteLine("GC.Collect() completed. Take 2nd heap shapshot and press Enter...");
            Console.ReadLine();

            Console.WriteLine("Run parallel mode (y/n)?");
            var consoleKey = ConsoleKey.A;
            while (consoleKey != ConsoleKey.Y && consoleKey != ConsoleKey.N)
                consoleKey = Console.ReadKey().Key;

            if (consoleKey == ConsoleKey.N)
                return;

            const int workerCnt = 10;
            using (var cancelSrc = new CancellationTokenSource(5000))
            {
                var tasks = new Task[workerCnt];
                for (var i = 0; i < workerCnt; i++)
                    tasks[i] = ClientWorkerLoop(address, ssl, "warmup", cancelSrc.Token);

                Task.WaitAll(tasks);
            }

            for (var i = 0; i < 3; i++)
                GC.Collect(2, GCCollectionMode.Forced, true, true);

            Console.WriteLine();
            Console.WriteLine($"Parallel mode warmup completed. Total connects: {_totalConnects}");
            Console.WriteLine("GC.Collect() completed. Take 3rd heap shapshot and press Enter...");
            Console.ReadLine();

            //const int workerCnt = 10;
            using (var cancelSrc = new CancellationTokenSource())
            {
                Console.WriteLine("Starting parallel mode. Press Enter to stop...");
                Thread.Sleep(500);

                var tasks = new Task[workerCnt];
                for (var i = 0; i < workerCnt; i++)
                    tasks[i] = ClientWorkerLoop(address, ssl, (i + 1).ToString(), cancelSrc.Token);

                Console.ReadLine();

                cancelSrc.Cancel();
                Task.WaitAll(tasks);
            }

            for (var i = 0; i < 3; i++)
                GC.Collect(2, GCCollectionMode.Forced, true, true);

            Console.WriteLine();
            Console.WriteLine($"Parallel mode stopped. Total connects: {_totalConnects}");
            Console.WriteLine("GC.Collect() completed. Take 4th heap shapshot and press Enter...");
            Console.ReadLine();
        }


        private static async Task ClientWorker(string address, bool ssl, string logPrefix)
        {
            var client = Program.CreateFunctionTestClient(address, ssl);
            var logName = $"{logPrefix}-{(ssl ? "ssl" : "raw")}";

            Console.WriteLine($"[{logName}] Connecting...");
            var res = await client.Channel.TryConnectAsync();
            Console.WriteLine($"[{logName}] Connect res: {res.Code} ({res.FaultMessage})");
            Interlocked.Increment(ref _totalConnects);

            const int callsCnt = 16;
            for (var i = 0; i < callsCnt; i++)
                await client.Async.TestCall1(10, "11");

            await client.Channel.CloseAsync();
        }

        private static async Task ClientWorkerLoop(string address, bool ssl, string logPrefix, CancellationToken cToken)
        {
            while (!cToken.IsCancellationRequested)
            {
                await ClientWorker(address, ssl, logPrefix);

                await Task.Delay(100);
            }
        }
    }
}
