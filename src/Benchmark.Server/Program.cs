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
            RunServer();
        }

        private static void RunServer()
        {
            var tcpEndpoint = new TcpServerEndpoint(812);
            tcpEndpoint.Serializer = new EntityMessageSerializer();

            var binding = new ServiceBinding(() => new BechmarkServiceImpl(), SerializerChoice.MessagePack);
            var server = new RpcServer(binding);
            server.AddEndpoint(tcpEndpoint);
            server.SetLogger(new ConsoleLogger(true, true));

            server.Start();

            Console.Read();

            server.StopAsync().Wait();
        }
    }
}
