using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using SharpRpc;
using TestCommon;

namespace TestClient
{
    class Program
    {
        static void Main(string[] args)
        {
            //var address = "c:\\temp\\shrpc.benchmark.uds"
            var address = args.FirstOrDefault() ?? "localhost";

            Console.Title = "#RPC Client";
            Console.WriteLine("SharpRpc test client.");
            Console.WriteLine("Framework: " + AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName);
            Console.WriteLine(GetAssemblyInfo(Assembly.GetExecutingAssembly()));
            Console.WriteLine(GetAssemblyInfo(typeof(RpcServer).Assembly));
            Console.WriteLine("Target server: " + address);
            Console.WriteLine("Choose action:");

            Console.WriteLine("1. Benchmark");
            Console.WriteLine("2. Function tests");
            Console.WriteLine("3. Keep connected");
            Console.WriteLine("4. Stress test");
            Console.WriteLine("5. Connection tests");
            Console.WriteLine("6. Load test");
            Console.WriteLine("7. Auth load test");
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
                var client = new BenchmarkClient("localhost", TcpSecurity.None);

                client.Channel.Opening += async (s, a) =>
                {
                    await client.Stub.Async.ApplyUpdate(new FooEntity());
                };

                client.Channel.Closing += async (s, a) =>
                {
                    await client.Stub.Async.ApplyUpdate(new FooEntity());
                };

                TimerCallback statusCheckAction = s =>
                {
                    Console.WriteLine("Channel.State = " + client.Stub.Channel.State);
                };

                var connectRet = client.Stub.Channel.TryConnectAsync().ToTask().Result;

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
            else if (choice == "4")
            {
                var test = new StressTest(address)
                {
                    MaxParallelRequests = 8,
                    MaxRequestsPerSession = 1000,
                    MaxItemsPerCall = 300,
                    ParallelConnections = 30
                };

                Console.WriteLine("Starting...");
                test.Start();
                Console.WriteLine("Test has been started. Press enter key to stop...");
                Console.Read();
                Console.WriteLine("Stopping...");
                test.Stop();
                Console.WriteLine("Test has been stopped. Errors count: " + test.Errors.Count);
                if (test.Errors.Count > 0)
                {
                    Console.WriteLine("Top 100 errors:");
                    test.PrintTopErrors(100);
                }
            }
            else if (choice == "5")
            {
                ConnectionTest.RunAll(address);
                Console.Read();
            }
            else if (choice == "6")
            {
                var loadTest = new LoadTest(address, 12);
                loadTest.Start();
                Console.WriteLine("Test has been started. Press enter key to stop...");
                Console.Read();
                loadTest.Stop();
                Console.WriteLine("Done.");
            }
            else if (choice == "7")
            {
                AuthLoadTest(address);
                Console.WriteLine("Done.");
                Console.Read();
            }
            else
                Console.WriteLine("Invalid input.");
        }

        private static string GetAssemblyInfo(Assembly assembly)
        {
            var aName = assembly.GetName();

            return aName.Name + ".dll, v" +  aName.Version + " (optimization " + IsOptimizationEbaled(assembly) + ")";
        }

        private static string IsOptimizationEbaled(Assembly assembly)
        {
            var attribute = assembly.GetCustomAttribute<DebuggableAttribute>();

            if (attribute == null)
                return "unknown";

            if (attribute.IsJITOptimizerDisabled)
                return "disabled";

            return "enabled";
        }

        private static void AuthLoadTest(string serverAddress)
        {
            var options = new ParallelOptions() { MaxDegreeOfParallelism = 10 };

            Parallel.For(0, 100000, options, i =>
            {
                var serviceName = "Bench/Ssl/Messagepack";
                var endpoint = new TcpClientEndpoint(serverAddress, serviceName, BenchmarkContractCfg.Port, new SslSecurity(NullCertValidator));
                endpoint.Credentials = new BasicCredentials("Admin", "zzzz11");
                var client = BenchmarkContract_Gen.CreateClient(endpoint, new BenchmarkClient.CallbackService());

                var result = client.Channel.TryConnectAsync().Result;

                if (result.Code != RpcRetCode.InvalidCredentials)
                {
                    //Debugger.Launch();
                    //Debugger.Break();
                    //throw new Exception("Assertion failed!");
                    Console.WriteLine($"assert code={result.Code} ");
                }
            });
        }

        private static bool NullCertValidator(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
