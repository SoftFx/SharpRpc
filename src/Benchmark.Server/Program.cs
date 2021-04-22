using Benchmark.Common;
using SharpRpc;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Benchmark.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            //RunSerializersBenchmark();
            RunServers();
        }

        private static void RunServers()
        {
            var srv1 = RunServer(ConcurrencyMode.NoQueue, 812);
            //var srv2 = RunServer(ConcurrencyMode.DataflowX1, 813);
            var srv3 = RunServer(ConcurrencyMode.PagedQueue, 814);

            Console.Read();

            srv1.StopAsync().Wait();
            //srv2.StopAsync().Wait();
            srv3.StopAsync().Wait();

            Console.Read();
        }

        private static RpcServer RunServer(ConcurrencyMode mode, int port)
        {
            var tcpEndpoint = new TcpServerEndpoint(port);
            BenchmarkContractCfg.ConfigureEndpoint(tcpEndpoint);
            tcpEndpoint.RxConcurrencyMode = mode;
            tcpEndpoint.Authenticator = new BasicClientAuthenticator(new AuthValidator());

            var server = new RpcServer();
            server.AddEndpoint(tcpEndpoint);
            server.BindService(BenchmarkContract_Gen.CreateBinding(() => new BechmarkServiceImpl()));
            server.SetLogger(new ConsoleLogger(true, true));
            server.Start();

            return server;
        }
    }
}
