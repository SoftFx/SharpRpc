﻿// Copyright © 2021 Soft-Fx. All rights reserved.
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

namespace TestServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = "#RPC Server";
            Console.WriteLine("SharpRpc test server.");
            Console.WriteLine("Framework: " + AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName);
            Console.WriteLine(GetAssemblyInfo(Assembly.GetExecutingAssembly()));
            Console.WriteLine(GetAssemblyInfo(typeof(RpcServer).Assembly));

            var srv1 = StartBenchmarkServer();
            var srv2 = StartFunctionTestServer();

            Console.Read();

            srv1.StopAsync().Wait();
            srv2.StopAsync().Wait();

            Console.Read();
        }

        private static RpcServer StartBenchmarkServer()
        {
            var tcpEndpoint = new TcpServerEndpoint(IPAddress.IPv6Any, BenchmarkContractCfg.GetPort(false), TcpServerSecurity.None);
            tcpEndpoint.IPv6Only = false;
            BenchmarkContractCfg.ConfigureEndpoint(tcpEndpoint);
            tcpEndpoint.Authenticator = new BasicAuthenticator(new AuthValidator());

            var serverCert = new StoredCertificate(StoreLocation.LocalMachine, StoreName.My, X509FindType.FindByThumbprint, "6e4c04ed965eb8d71a66b8e2b89e5767f2e076d8");
            var sslEndpoit = new TcpServerEndpoint(IPAddress.IPv6Any, BenchmarkContractCfg.GetPort(true), new SslServerSecurity(serverCert));
            sslEndpoit.IPv6Only = false;
            BenchmarkContractCfg.ConfigureEndpoint(sslEndpoit);
            sslEndpoit.Authenticator = new BasicAuthenticator(new AuthValidator());

            var multicaster = new FooMulticaster();

            var server = new RpcServer(BenchmarkContract_Gen.CreateBinding(() => new BenchmarkServiceImpl(multicaster)));
            server.AddEndpoint(tcpEndpoint);
            server.AddEndpoint(sslEndpoit);
            server.SetLogger(new ConsoleLogger(true, true));
            server.Start();

            return server;
        }

        private static RpcServer StartFunctionTestServer()
        {
            var tcpEndpoint = new TcpServerEndpoint(IPAddress.IPv6Any, 812, TcpServerSecurity.None);
            tcpEndpoint.IPv6Only = false;

            var server = new RpcServer(FunctionTestContract_Gen.CreateBinding(() => new FunctionTestService()));
            server.AddEndpoint(tcpEndpoint);
            server.SetLogger(new ConsoleLogger(true, true));
            server.Start();

            return server;
        }

        private static string GetAssemblyInfo(Assembly assembly)
        {
            var aName = assembly.GetName();

            return aName.Name + ".dll, v" + aName.Version + " (optimization " + IsOptimizationEbaled(assembly) + ")";
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
    }
}
