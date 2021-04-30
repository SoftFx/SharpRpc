// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Benchmark.Common;
using SharpRpc;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Benchmark.Server
{
    class Program
    {
        static void Main(string[] args)
        {
            //RunSerializersBenchmark();
            RunServers();
        }

        private static void RunServers()
        {
            var srv1 = RunServer(812, 813);

            Console.Read();

            srv1.StopAsync().Wait();

            Console.Read();
        }

        private static RpcServer RunServer(int port, int sslPort)
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
    }
}
