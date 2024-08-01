using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Runtime.Remoting;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using SharpRpc;
using TestCommon;
using static TestClient.FunctionTest;

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
            Console.WriteLine("5. Load test");
            Console.WriteLine("6. Auth load test");
            Console.WriteLine("7. Serialization erorr handling tests");
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
                var client = new BenchmarkClient(address, TcpSecurity.None, true);

                client.Channel.InitializingSession += async (s, a) =>
                {
                    await client.Stub.Async.ApplyUpdate(new FooEntity());
                };

                client.Channel.DeinitializingSession += async (s, a) =>
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
                        Console.WriteLine("Disconnected. ExitCode=" + client.Stub.Channel.Fault.Code);
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
                var loadTest = new LoadTest(address, 12);
                loadTest.Start();
                Console.WriteLine("Test has been started. Press enter key to stop...");
                Console.Read();
                loadTest.Stop();
                Console.WriteLine("Done.");
            }
            else if (choice == "6")
            {
                AuthLoadTest(address);
                Console.WriteLine("GC.Collect...");
                GC.Collect(2, GCCollectionMode.Forced, true);

                Console.WriteLine("Done.");
                Console.Read();
            }
            else if (choice == "7")
                TestSerializationErrorHandling(address);
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
                    Console.WriteLine($"assert code={result.Code} ");
            });
        }

        private static void TestSerializationErrorHandling(string address)
        {
            //Console.WriteLine("Broken response:");
            //TestBrokenResponse(address);
            //Console.WriteLine("Broken request:");
            //TestBrokenRequest(address);
            Console.WriteLine("Broken output item:");
            TestBrokenResponse(address);
        }

        private static void TestBrokenResponse(string address)
        {
            var client = CreateFunctionTestClient(address, false);

            var resp = client.Try.BrokenResponse();

            Console.WriteLine(resp.Code);

            while (client.Channel.State != ChannelState.Faulted)
            {
                Console.WriteLine(client.Channel.State);
                Task.Delay(1000).Wait();
            }

            Console.WriteLine(client.Channel.State);

            Console.ReadLine();
        }

        private static void TestBrokenRequest(string address)
        {
            var client = CreateFunctionTestClient(address, false);

            var resp = client.Try.BrokenRequest(new BrokenEntity());

            Console.WriteLine(resp.Code);

            while (client.Channel.State != ChannelState.Faulted)
            {
                Console.WriteLine(client.Channel.State);
                Task.Delay(1000).Wait();
            }

            Console.WriteLine(client.Channel.State);

            Console.ReadLine();
        }

        private static void TestBrokenOutputStream(string address)
        {
            var client = CreateFunctionTestClient(address, false);

            var resp = client.Try.BrokenResponse();

            Console.WriteLine(resp.Code);

            while (client.Channel.State != ChannelState.Faulted)
            {
                Console.WriteLine(client.Channel.State);
                Task.Delay(1000).Wait();
            }

            Console.WriteLine(client.Channel.State);

            Console.ReadLine();
        }

        private static FunctionTestContract_Gen.Client CreateFunctionTestClient(string address, bool ssl)
        {
            var security = ssl ? new SslSecurity(NullCertValidator) : TcpSecurity.None;
            var port = 812;
            var serviceName = ssl ? "func/ssl" : "func";
            var endpoint = new TcpClientEndpoint(new DnsEndPoint(address, port), serviceName, security);

            if (ssl)
                endpoint.Credentials = new BasicCredentials("Admin", "zzzz");

            var callback = new CallbackHandler();
            return FunctionTestContract_Gen.CreateClient(endpoint, callback);
        }

        private static bool NullCertValidator(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
