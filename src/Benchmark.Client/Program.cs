using Benchmark.Common;
using SharpRpc;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Benchmark.Client
{
    class Program
    {
        static void Main(string[] args)
        {
            var address = args.FirstOrDefault() ?? "localhost";

            Console.WriteLine("SharpRpc test client. Server address: " + address);
            Console.WriteLine("Choose action:");

            Console.WriteLine("1. Benchmark");
            Console.WriteLine("2. Keep connected");
            Console.Write(">");

            var choice = Console.ReadLine();

            if (choice == "1")
            {
                Benchmark.LaunchTestSeries(address);
                Console.Read();
            }
            else if (choice == "2")
            {
                var endpoint = new TcpClientEndpoint("localhost", 812, TcpSecurity.None);
                BenchmarkContractCfg.ConfigureEndpoint(endpoint);
                var client = BenchmarkContract_Gen.CreateClient(endpoint);
                var connectRet = client.Channel.TryConnectAsync().AsTask().Result;

                TimerCallback statusCheckAction = s =>
                {
                    Console.WriteLine("Channel.State = " + client.Channel.State);
                };

                if (connectRet.Code == RpcRetCode.Ok)
                {
                    using (var timer = new Timer(statusCheckAction, null, 5000, 5000))
                    {
                        Console.WriteLine("Connected. Press any key to disconnect.");
                        Console.Read();

                        client.Channel.CloseAsync().Wait();
                    }
                }
                else
                    Console.WriteLine("Failed to connect! Code: {0} Message: {1}", connectRet.Code, connectRet.Fault.Message);
            }
            else
                Console.WriteLine("Invalid input.");
        }
    }
}