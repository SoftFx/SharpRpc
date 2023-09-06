// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using SharpRpc;
using TestCommon;
using TestCommon.StressTest;

namespace TestServer
{
    class Program
    {
        private const string CertThumbprint = "ad1a42f5598388af3d656a9a03ebf01823995f5a"; //"6e4c04ed965eb8d71a66b8e2b89e5767f2e076d8"

        static void Main(string[] args)
        {
            Console.Title = "#RPC Server";
            Console.WriteLine("SharpRpc test server.");
            Console.WriteLine("Framework: " + AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName);
            Console.WriteLine(GetAssemblyInfo(Assembly.GetExecutingAssembly()));
            Console.WriteLine(GetAssemblyInfo(typeof(RpcServer).Assembly));

            try
            {
                var srv1 = StartBenchmarkServer();
                var srv2 = StartFunctionTestServer();
                var srv3 = StartStressServer();

                Console.Read();

                srv1.StopAsync().Wait();
                srv2.StopAsync().Wait();
                srv3.StopAsync().Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            Console.Read();
        }

        private static RpcServer StartBenchmarkServer()
        {
            var multicaster = new FooMulticaster();
            var serviceDescriptor = BenchmarkContract_Gen.CreateServiceDescriptor(() => new BenchmarkServiceImpl(multicaster));
            var serverCert = new StoredCertificate(StoreLocation.LocalMachine, StoreName.My, X509FindType.FindByThumbprint, CertThumbprint);

            var tcpEndpoint = new TcpServerEndpoint(IPAddress.IPv6Any, BenchmarkContractCfg.Port);
            tcpEndpoint.IPv6Only = false;
            BenchmarkContractCfg.ConfigureEndpoint(tcpEndpoint);

            tcpEndpoint.BindService("Bench/Messagepack", serviceDescriptor)
                .SetAuthenticator(new BasicAuthenticator(new AuthValidator()));

            tcpEndpoint.BindService("Bench/SSL/Messagepack", serviceDescriptor)
                .SetSecurity(new SslServerSecurity(serverCert))
                .SetAuthenticator(new BasicAuthenticator(new AuthValidator()));

            var server = new RpcServer();
            server.AddEndpoint(tcpEndpoint);

#if NET5_0_OR_GREATER
            var udsEndpoint = new UdsServerEndpoint("c:\\temp\\shrpc.benchmark.uds");
            udsEndpoint.BindService(serviceDescriptor)
                .SetAuthenticator(new BasicAuthenticator(new AuthValidator()));
            BenchmarkContractCfg.ConfigureEndpoint(udsEndpoint);
            server.AddEndpoint(udsEndpoint);
#endif

            server.SetLogger(new ConsoleLogger(true, true));
            server.Start();

            return server;
        }

        private static RpcServer StartFunctionTestServer()
        {
            var serverCert = new StoredCertificate(StoreLocation.LocalMachine, StoreName.My, X509FindType.FindByThumbprint, CertThumbprint);
            var descriptor = FunctionTestContract_Gen.CreateServiceDescriptor(() => new FunctionTestService());

            var tcpEndpoint = new TcpServerEndpoint(IPAddress.IPv6Any, 812);
            tcpEndpoint.IPv6Only = false;

            tcpEndpoint.BindService("func", descriptor);
            tcpEndpoint.BindService("func/ssl", descriptor)
                .SetSecurity(new SslServerSecurity(serverCert))
                .SetAuthenticator(new BasicAuthenticator(new AuthValidator()));

            var server = new RpcServer();
            server.AddEndpoint(tcpEndpoint);
            server.SetLogger(new ConsoleLogger(true, true));
            server.Start();

            return server;
        }

        private static RpcServer StartStressServer()
        {
            var descriptor = StressTestContract_Gen.CreateServiceDescriptor(() => new StressTestService());

            var tcpEndpoint = new TcpServerEndpoint(IPAddress.IPv6Any, 813);
            tcpEndpoint.IPv6Only = false;
            tcpEndpoint.BindService(descriptor);

            var server = new RpcServer();
            server.AddEndpoint(tcpEndpoint);
            //server.SetLogger(new ConsoleLogger(false, true));
            server.Start();

            return server;
        }

        private static string GetAssemblyInfo(Assembly assembly)
        {
            var aName = assembly.GetName();

            return aName.Name + ".dll, v" + aName.Version + " (optimization " + IsOptimizationEnabled(assembly) + ")";
        }

        private static string IsOptimizationEnabled(Assembly assembly)
        {
            var attribute = assembly.GetCustomAttribute<DebuggableAttribute>();

            if (attribute == null)
                return "unknown";

            if (attribute.IsJITOptimizerDisabled)
                return "disabled";

            return "enabled";
        }
    }
}
