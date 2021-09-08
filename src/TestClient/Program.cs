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

            Console.Title = "#RPC Client";
            Console.WriteLine("SharpRpc test client.");
            Console.WriteLine("Framework: " + AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName);
            Console.WriteLine("Target server: " + address);
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

                var multiplier = 0;

                if (mChoice == "1")
                    multiplier = 1;
                else if (mChoice == "2")
                    multiplier = 10;
                else if (mChoice == "3")
                    multiplier = 40;
                else
                    Console.WriteLine("Invalid input.");

                if (multiplier > 0)
                {
                    var benchmark = new Benchmark(address);
                    benchmark.LaunchTestSeries(multiplier);
                    benchmark.PrintReportToConsole();
                    benchmark.SaveReportToFile();

                    Console.WriteLine("Done. Press enty key to exit...");
                    Console.Read();
                }
            }
            else if (choice == "2")
            {
                FunctionTest.Run(address);
                Console.Read();
            }
            else if (choice == "3")
            {   
                var client = new BenchmarkClient("localhost", 812, TcpSecurity.None);
                var connectRet = client.Stub.Channel.TryConnectAsync().ToTask().Result;

                TimerCallback statusCheckAction = s =>
                {
                    Console.WriteLine("Channel.State = " + client.Stub.Channel.State);
                };

                if (connectRet.Code == RpcRetCode.Ok)
                {
                    using (var timer = new Timer(statusCheckAction, null, 5000, 5000))
                    {
                        Console.WriteLine("Connected. Press any key to disconnect.");
                        Console.Read();

                        client.Stub.Channel.CloseAsync().Wait();
                    }
                }
                else
                    Console.WriteLine("Failed to connect! Code: {0} Message: {1}", connectRet.Code, connectRet.FaultMessage);
            }
            else
                Console.WriteLine("Invalid input.");
        }
    }
}
