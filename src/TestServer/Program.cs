// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using SharpRpc;
using TestCommon;

namespace TestServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var srv1 = StartBenchmarkServer();
            var srv2 = StartFunctionTestServer();

            Console.Read();

            srv1.StopAsync().Wait();
            srv2.StopAsync().Wait();

            Console.Read();
        }

        private static RpcServer StartBenchmarkServer()
        {
            var tcpEndpoint = new TcpServerEndpoint(IPAddress.Any, BenchmarkContractCfg.GetPort(false), TcpServerSecurity.None);
            BenchmarkContractCfg.ConfigureEndpoint(tcpEndpoint);
            tcpEndpoint.Authenticator = new BasicAuthenticator(new AuthValidator());

            var serverCert = new StoredCertificate(StoreLocation.LocalMachine, StoreName.My, X509FindType.FindByThumbprint, "‎6e4c04ed965eb8d71a66b8e2b89e5767f2e076d8");
            var sslEndpoit = new TcpServerEndpoint(IPAddress.Any, BenchmarkContractCfg.GetPort(true), new SslServerSecurity(serverCert));
            BenchmarkContractCfg.ConfigureEndpoint(sslEndpoit);
            sslEndpoit.Authenticator = new BasicAuthenticator(new AuthValidator());

            var server = new RpcServer(BenchmarkContract_Gen.CreateBinding(() => new BechmarkServiceImpl()));
            server.AddEndpoint(tcpEndpoint);
            server.AddEndpoint(sslEndpoit);
            server.SetLogger(new ConsoleLogger(true, true));
            server.Start();

            return server;
        }

        private static RpcServer StartFunctionTestServer()
        {
            var tcpEndpoint = new TcpServerEndpoint(IPAddress.IPv6Loopback, 812, TcpServerSecurity.None);

            var server = new RpcServer(FunctionTestContract_Gen.CreateBinding(() => new FunctionTestService()));
            server.AddEndpoint(tcpEndpoint);
            server.SetLogger(new ConsoleLogger(true, true));
            server.Start();

            return server;
        }
    }
}
