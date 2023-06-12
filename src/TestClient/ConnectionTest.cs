// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using TestClient.TestLib;
using TestCommon;

namespace TestClient
{
    internal abstract class ConnectionTest : TestBase
    {
        public static void RunAll(string address)
        {
            var runner = new TestRunner();
            AddCases(runner, address, false, true, true);
            AddCases(runner, address, true, true, true);
            AddCases(runner, address, false, false, false);
            AddCases(runner, address, true, false, false);
            runner.RunAll();
        }

        public static void AddCases(TestRunner runner, string address, bool ssl, bool actual, bool preconnect)
        {
            if (!actual)
                address += "11";

            Func<FunctionTestContract_Gen.Client> factory = () => CreateConnection(address, ssl, actual, preconnect);

            var nameBuilder = new StringBuilder()
                .Append(address);
            if (ssl)
                nameBuilder.Append(" SSL");
            if (preconnect)
                nameBuilder.Append(" Preconnected");
            else
                nameBuilder.Append(" Autoconnect");

            runner.AddCases(new InputStreamConnectTest().GetCases(nameBuilder.ToString(), preconnect, actual, factory));
        }

        public override void RunTest(TestCase tCase)
        {
            var clientFactory = tCase.GetParam<Func<FunctionTestContract_Gen.Client>>("clientFactory");
            var client = clientFactory();
            var preconnect = tCase.GetParam<bool>("preconnect");
            var existingAddress = tCase.GetParam<bool>("existingAddress");

            if (preconnect)
            {
                var connectResult = client.Channel.TryConnectAsync().Result;
                if (!connectResult.IsOk)
                    throw new Exception("Failed to connect: " + connectResult.FaultMessage);
            }

            try
            {
                RunTest(client, existingAddress);
            }
            finally
            {
                try
                {
                    client.Channel.CloseAsync().Wait();
                }
                catch { }
            }
        }

        protected abstract void RunTest(FunctionTestContract_Gen.Client client, bool existingAddress);

        private static FunctionTestContract_Gen.Client CreateConnection(string address, bool ssl, bool actual, bool preconnect)
        {
            var security = ssl ? new SslSecurity(NullCertValidator) : TcpSecurity.None;
            var port = 812;
            var serviceName = ssl ? "func/ssl" : "func";
            var endpoint = new TcpClientEndpoint(new DnsEndPoint(address, port), serviceName, security);

            if (ssl)
                endpoint.Credentials = new BasicCredentials("Admin", "zzzz");

            var callback = new FunctionTest.CallbackHandler();
            return FunctionTestContract_Gen.CreateClient(endpoint, callback);
        }

        private static bool NullCertValidator(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }

    internal class InputStreamConnectTest : ConnectionTest
    {
        public IEnumerable<TestCase> GetCases(string caseName, bool preconnect, bool existingAddress,
            Func<FunctionTestContract_Gen.Client> clientFactory)
        {
            yield return new TestCase(this)
                .SetHiddenParam("clientFactory", clientFactory)
                .SetHiddenParam("preconnect", preconnect)
                .SetHiddenParam("existingAddress", existingAddress)
                .SetParam("Name", caseName);
        }

        protected override void RunTest(FunctionTestContract_Gen.Client client, bool existingAddress)
        {
            try
            {
                var callObj = client.TestOutStream(new SharpRpc.StreamOptions(), TimeSpan.Zero, 1600, StreamTestOptions.None);

                var e = callObj.OutputStream.GetEnumerator();
                var count = 0;

                while (e.MoveNextAsync().Result)
                    count++;

                if (count != 1600)
                    throw new Exception();
            }
            catch (RpcException ex)
            {
                if (ex.ErrorCode == RpcRetCode.HostNotFound && !existingAddress)
                    return;

                throw;
            }
        }
    }
}
