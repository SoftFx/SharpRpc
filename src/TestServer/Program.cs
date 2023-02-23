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
        private const string CertThumbprint = "d58b3c94d39c43aecf1750280db0c4935ed53de8"; //"6e4c04ed965eb8d71a66b8e2b89e5767f2e076d8"

        static void Main(string[] args)
        {
            Console.Title = "#RPC Server";
            Console.WriteLine("SharpRpc test server.");
            Console.WriteLine("Framework: " + AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName);
            Console.WriteLine(GetAssemblyInfo(Assembly.GetExecutingAssembly()));
            Console.WriteLine(GetAssemblyInfo(typeof(RpcServer).Assembly));

            var srv1 = StartBenchmarkServer();
            var srv2 = StartFunctionTestServer();
            var srv3 = StartStressServer();

            Console.Read();

            srv1.StopAsync().Wait();
            srv2.StopAsync().Wait();
            srv3.StopAsync().Wait();

            Console.Read();
        }

        private static RpcServer StartBenchmarkServer()
        {
            var tcpEndpoint = new TcpServerEndpoint(IPAddress.IPv6Any, BenchmarkContractCfg.GetPort(false), TcpServerSecurity.None);
            tcpEndpoint.IPv6Only = false;
            BenchmarkContractCfg.ConfigureEndpoint(tcpEndpoint);
            tcpEndpoint.Authenticator = new BasicAuthenticator(new AuthValidator());

            var serverCert = new StoredCertificate(StoreLocation.LocalMachine, StoreName.My, X509FindType.FindByThumbprint, CertThumbprint);
            var sslEndpoint = new TcpServerEndpoint(IPAddress.IPv6Any, BenchmarkContractCfg.GetPort(true), new SslServerSecurity(serverCert));
            sslEndpoint.IPv6Only = false;
            BenchmarkContractCfg.ConfigureEndpoint(sslEndpoint);
            sslEndpoint.Authenticator = new BasicAuthenticator(new AuthValidator());

            var multicaster = new FooMulticaster();

            var server = new RpcServer(BenchmarkContract_Gen.CreateBinding(() => new BenchmarkServiceImpl(multicaster)));
            server.AddEndpoint(tcpEndpoint);
            server.AddEndpoint(sslEndpoint);

#if NET5_0_OR_GREATER
            var udsEndpoint = new UdsServerEndpoint("c:\\temp\\shrpc.benchmark.uds", TcpServerSecurity.None);
            BenchmarkContractCfg.ConfigureEndpoint(udsEndpoint);
            udsEndpoint.Authenticator = new BasicAuthenticator(new AuthValidator());
            server.AddEndpoint(udsEndpoint);
#endif

            server.SetLogger(new ConsoleLogger(true, true));
            server.Start();

            return server;
        }

        private static RpcServer StartFunctionTestServer()
        {
            var tcpEndpoint = new TcpServerEndpoint(IPAddress.IPv6Any, 812, TcpServerSecurity.None);
            tcpEndpoint.IPv6Only = false;

            var serverCert = new StoredCertificate(StoreLocation.LocalMachine, StoreName.My, X509FindType.FindByThumbprint, CertThumbprint);
            var sslEndpoint = new TcpServerEndpoint(IPAddress.IPv6Any, 814, new SslServerSecurity(serverCert));
            sslEndpoint.Authenticator = new BasicAuthenticator(new AuthValidator());

            var server = new RpcServer(FunctionTestContract_Gen.CreateBinding(() => new FunctionTestService()));
            server.AddEndpoint(tcpEndpoint);
            server.AddEndpoint(sslEndpoint);
            server.SetLogger(new ConsoleLogger(true, true));
            server.Start();

            return server;
        }

        private static RpcServer StartStressServer()
        {
            var tcpEndpoint = new TcpServerEndpoint(IPAddress.IPv6Any, 813, TcpServerSecurity.None);
            tcpEndpoint.IPv6Only = false;

            var server = new RpcServer(StressTestContract_Gen.CreateBinding(() => new StressTestService()));
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
