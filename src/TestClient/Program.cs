using System;
using System.Linq;
using System.Threading;
using SharpRpc;
using TestCommon;

namespace TestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            var address = args.FirstOrDefault() ?? "localhost";

            Console.WriteLine("SharpRpc test client. Server address: " + address);
            Console.WriteLine("Choose action:");

            Console.WriteLine("1. Benchmark");
            Console.WriteLine("2. Function test");
            Console.WriteLine("3. Keep connected");
            Console.Write(">");

            var choice = Console.ReadLine();

            if (choice == "1")
            {
                Console.WriteLine("Choose multiplier:");

                Console.WriteLine("1. x1");
                Console.WriteLine("2. x10");
                Console.WriteLine("3. x40");
                Console.Write(">");

                var mChoice = Console.ReadLine();

                if (mChoice == "1")
                    Benchmark.LaunchTestSeries(address, 1);
                else if (mChoice == "2")
                    Benchmark.LaunchTestSeries(address, 10);
                else if (mChoice == "3")
                    Benchmark.LaunchTestSeries(address, 40);
                else
                    Console.WriteLine("Invalid input.");
                Console.Read();
            }
            else if (choice == "2")
            {
                FunctionTest.Run(address);
                Console.Read();
            }
            else if (choice == "3")
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
