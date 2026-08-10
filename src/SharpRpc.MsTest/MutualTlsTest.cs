// Copyright Â© 2026 Soft-Fx. All rights reserved.
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpRpc.Tcp;
using System;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc.MsTest
{
    [TestClass]
    public class MutualTlsTest
    {
        private const string ServerAuthOid = "1.3.6.1.5.5.7.3.1";
        private const string ClientAuthOid = "1.3.6.1.5.5.7.3.2";

        private static X509Certificate2 _serverCert;
        private static X509Certificate2 _clientCert;

        [ClassInitialize]
        public static void Init(TestContext context)
        {
            _serverCert = CreateSelfSigned("SharpRpc mTLS test server", ServerAuthOid);
            _clientCert = CreateSelfSigned("SharpRpc mTLS test client", ClientAuthOid);
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            _serverCert?.Dispose();
            _clientCert?.Dispose();
        }

        [TestMethod]
        public async Task MutualTls_ValidClientCert_HandshakeSucceeds()
        {
            string validatedThumbprint = null;

            var serverSecurity = new SslServerSecurity(_serverCert)
            {
                Protocols = SslProtocols.Tls12,
                RequireClientCertificate = true,
                ClientCertificateValidator = (sender, cert, chain, errors) =>
                {
                    validatedThumbprint = (cert as X509Certificate2)?.Thumbprint ?? cert?.GetCertHashString();
                    return cert != null && cert.GetCertHashString() == _clientCert.GetCertHashString();
                },
            };

            var clientSecurity = new SslSecurity(AcceptAnyServerCert)
            {
                Protocols = SslProtocols.Tls12,
                ClientCertificates = new X509CertificateCollection { _clientCert },
            };

            var result = await DoHandshake(serverSecurity, clientSecurity);
            try
            {
                Assert.IsNull(result.ServerError, "Server handshake failed: " + result.ServerError?.Message);
                Assert.IsNull(result.ClientError, "Client handshake failed: " + result.ClientError?.Message);
                Assert.AreEqual(_clientCert.GetCertHashString(), validatedThumbprint);

                var serverInfo = result.ServerTransport.GetInfo() as SslConnectionInfo;
                Assert.IsNotNull(serverInfo, "Server transport info must be SslConnectionInfo.");
                Assert.IsNotNull(serverInfo.RemoteCertificate, "Client certificate must be exposed in the server transport info.");
                Assert.AreEqual(_clientCert.Thumbprint, serverInfo.RemoteCertificate.Thumbprint);

                var clientInfo = result.ClientTransport.GetInfo() as SslConnectionInfo;
                Assert.IsNotNull(clientInfo, "Client transport info must be SslConnectionInfo.");
                Assert.IsNotNull(clientInfo.RemoteCertificate, "Server certificate must be exposed in the client transport info.");
                Assert.AreEqual(_serverCert.Thumbprint, clientInfo.RemoteCertificate.Thumbprint);

                await AssertDataFlows(result.ServerTransport, result.ClientTransport);
            }
            finally
            {
                await result.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task MutualTls_RejectedClientCert_HandshakeFails()
        {
            var serverSecurity = new SslServerSecurity(_serverCert)
            {
                Protocols = SslProtocols.Tls12,
                RequireClientCertificate = true,
                ClientCertificateValidator = (sender, cert, chain, errors) => false,
            };

            var clientSecurity = new SslSecurity(AcceptAnyServerCert)
            {
                Protocols = SslProtocols.Tls12,
                ClientCertificates = new X509CertificateCollection { _clientCert },
            };

            var result = await DoHandshake(serverSecurity, clientSecurity);
            try
            {
                Assert.IsNotNull(result.ServerError, "Server handshake must fail when the validator rejects the certificate.");
                var rpcEx = result.ServerError as RpcException;
                Assert.IsNotNull(rpcEx, "Server handshake failure must surface as RpcException, but was: " + result.ServerError);
                Assert.AreEqual(RpcRetCode.InvalidCredentials, rpcEx.ErrorCode);

                // On TLS 1.2 the client may complete its side of the handshake before the server's
                // rejection alert arrives; the failure then surfaces on the first read.
                if (result.ClientError == null)
                    await AssertConnectionDead(result.ClientTransport);
            }
            finally
            {
                await result.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task MutualTls_MissingClientCert_ValidatorSeesNullAndRejects()
        {
            var validatorInvoked = false;
            X509Certificate seenCert = null;
            var seenErrors = SslPolicyErrors.None;

            var serverSecurity = new SslServerSecurity(_serverCert)
            {
                Protocols = SslProtocols.Tls12,
                RequireClientCertificate = true,
                ClientCertificateValidator = (sender, cert, chain, errors) =>
                {
                    validatorInvoked = true;
                    seenCert = cert;
                    seenErrors = errors;
                    return cert != null;
                },
            };

            var clientSecurity = new SslSecurity(AcceptAnyServerCert)
            {
                Protocols = SslProtocols.Tls12,
            };

            var result = await DoHandshake(serverSecurity, clientSecurity);
            try
            {
                Assert.IsNotNull(result.ServerError, "Server handshake must fail when no client certificate is provided.");

                // Pins the semantics the validator must handle: a missing certificate does not fail the
                // handshake automatically - the validator is invoked with null and must return false.
                if (validatorInvoked)
                {
                    Assert.IsNull(seenCert);
                    Assert.IsTrue((seenErrors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0);
                }
            }
            finally
            {
                await result.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task DefaultOptions_OneWayTls_BehaviorUnchanged()
        {
            var serverSecurity = new SslServerSecurity(_serverCert) { Protocols = SslProtocols.Tls12 };
            var clientSecurity = new SslSecurity(AcceptAnyServerCert) { Protocols = SslProtocols.Tls12 };

            var result = await DoHandshake(serverSecurity, clientSecurity);
            try
            {
                Assert.IsNull(result.ServerError, "Server handshake failed: " + result.ServerError?.Message);
                Assert.IsNull(result.ClientError, "Client handshake failed: " + result.ClientError?.Message);

                var serverInfo = result.ServerTransport.GetInfo() as SslConnectionInfo;
                Assert.IsNotNull(serverInfo);
                Assert.IsNull(serverInfo.RemoteCertificate, "No client certificate must be present in one-way TLS.");

                await AssertDataFlows(result.ServerTransport, result.ClientTransport);
            }
            finally
            {
                await result.DisposeAsync();
            }
        }

        [TestMethod]
        public async Task ClientCertConfigured_ServerDoesNotRequest_CertNotSent()
        {
            var serverSecurity = new SslServerSecurity(_serverCert) { Protocols = SslProtocols.Tls12 };

            var clientSecurity = new SslSecurity(AcceptAnyServerCert)
            {
                Protocols = SslProtocols.Tls12,
                ClientCertificates = new X509CertificateCollection { _clientCert },
            };

            var result = await DoHandshake(serverSecurity, clientSecurity);
            try
            {
                Assert.IsNull(result.ServerError, "Server handshake failed: " + result.ServerError?.Message);
                Assert.IsNull(result.ClientError, "Client handshake failed: " + result.ClientError?.Message);

                var serverInfo = result.ServerTransport.GetInfo() as SslConnectionInfo;
                Assert.IsNotNull(serverInfo);
                Assert.IsNull(serverInfo.RemoteCertificate, "The client certificate must not be transmitted unless the server requests it.");
            }
            finally
            {
                await result.DisposeAsync();
            }
        }

        [TestMethod]
        public void CertSelection_IssuerMatch_PicksMatchingCert()
        {
            var certs = new X509CertificateCollection { _serverCert, _clientCert };
            var acceptableIssuers = new[] { _clientCert.Issuer };

            var selected = SslSecurity.SelectClientCertificate(null, "host", certs, null, acceptableIssuers);

            Assert.AreSame(_clientCert, selected);
        }

        [TestMethod]
        public void CertSelection_NoIssuerMatch_FallsBackToFirstCert()
        {
            var certs = new X509CertificateCollection { _clientCert };
            var acceptableIssuers = new[] { "CN=Some Unrelated CA" };

            var selected = SslSecurity.SelectClientCertificate(null, "host", certs, null, acceptableIssuers);

            Assert.AreSame(_clientCert, selected);
        }

        [TestMethod]
        public void CertSelection_NoIssuersProvided_PicksFirstCert()
        {
            var certs = new X509CertificateCollection { _clientCert, _serverCert };

            var selected = SslSecurity.SelectClientCertificate(null, "host", certs, null, null);

            Assert.AreSame(_clientCert, selected);
        }

        [TestMethod]
        public void CertSelection_NoCerts_ReturnsNull()
        {
            Assert.IsNull(SslSecurity.SelectClientCertificate(null, "host", null, null, null));
            Assert.IsNull(SslSecurity.SelectClientCertificate(null, "host", new X509CertificateCollection(), null, null));
        }

        #region Handshake plumbing

        private class HandshakeResult
        {
            public ByteTransport ServerTransport;
            public ByteTransport ClientTransport;
            public Exception ServerError;
            public Exception ClientError;
            public Socket ServerSocket;
            public Socket ClientSocket;

            public async Task DisposeAsync()
            {
                if (ServerTransport != null)
                    await CloseTransport(ServerTransport);
                else
                    CloseSocket(ServerSocket);

                if (ClientTransport != null)
                    await CloseTransport(ClientTransport);
                else
                    CloseSocket(ClientSocket);
            }

            private static async Task CloseTransport(ByteTransport transport)
            {
                try { await transport.Shutdown(); } catch { }
                try { await transport.DisposeAsync(); } catch { }
            }

            private static void CloseSocket(Socket socket)
            {
                try { socket?.Dispose(); } catch { }
            }
        }

        private static async Task<HandshakeResult> DoHandshake(SslServerSecurity serverSecurity, SslSecurity clientSecurity)
        {
            var result = new HandshakeResult();

            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                var listener = new TcpListener(IPAddress.Loopback, 0);
                listener.Start();
                try
                {
                    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
                    result.ClientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    var connectTask = result.ClientSocket.ConnectAsync(IPAddress.Loopback, port);
                    result.ServerSocket = await listener.AcceptSocketAsync();
                    await connectTask;
                }
                finally
                {
                    listener.Stop();
                }

                var serverTask = Task.Run(async () =>
                {
                    var unsecured = new SocketTransport(result.ServerSocket, new TaskFactory(), "server", NullLogger.Instance);
                    return await serverSecurity.SecureTransport(unsecured, null, "server", NullLogger.Instance, timeout.Token);
                });

                // A unique target host per handshake prevents Schannel TLS session resumption from
                // carrying state (including the client certificate) between tests in this process.
                var targetHost = "sharprpc-mtls-test-" + Guid.NewGuid().ToString("N");

                var clientTask = Task.Run(async () =>
                {
                    return await clientSecurity.SecureTransport(result.ClientSocket, null, targetHost, "client", NullLogger.Instance, timeout.Token);
                });

                try { result.ServerTransport = await serverTask; }
                catch (Exception ex) { result.ServerError = ex; CloseSocketOnFailure(result.ServerSocket); }

                try { result.ClientTransport = await clientTask; }
                catch (Exception ex) { result.ClientError = ex; CloseSocketOnFailure(result.ClientSocket); }
            }

            return result;
        }

        private static void CloseSocketOnFailure(Socket socket)
        {
            // A failed handshake may leave the peer's handshake pending forever; abort the connection
            // so the other side's await completes.
            try { socket?.Dispose(); } catch { }
        }

        private static async Task AssertDataFlows(ByteTransport serverTransport, ByteTransport clientTransport)
        {
            var payload = new byte[] { 1, 2, 3, 4, 5 };

            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                await clientTransport.Send(new ArraySegment<byte>(payload), timeout.Token);

                var received = new byte[payload.Length];
                var totalRead = 0;
                while (totalRead < received.Length)
                {
                    var read = await serverTransport.Receive(new ArraySegment<byte>(received, totalRead, received.Length - totalRead), timeout.Token);
                    Assert.IsTrue(read > 0, "Connection was closed before all data arrived.");
                    totalRead += read;
                }

                CollectionAssert.AreEqual(payload, received);
            }
        }

        private static async Task AssertConnectionDead(ByteTransport transport)
        {
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                try
                {
                    var read = await transport.Receive(new ArraySegment<byte>(new byte[1]), timeout.Token);
                    Assert.AreEqual(0, read, "The connection must be dead after the server rejected the handshake.");
                }
                catch (Exception)
                {
                    // Expected: the server's rejection surfaces as an error on the first read.
                }
            }
        }

        private static bool AcceptAnyServerCert(object sender, X509Certificate cert, X509Chain chain, SslPolicyErrors errors) => true;

        private static X509Certificate2 CreateSelfSigned(string cn, string ekuOid)
        {
            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest("CN=" + cn, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
                request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new Oid(ekuOid) }, false));

                var sanBuilder = new SubjectAlternativeNameBuilder();
                sanBuilder.AddDnsName("localhost");
                request.CertificateExtensions.Add(sanBuilder.Build());

                using (var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(2)))
                {
                    // Re-import via PFX: Schannel cannot use the ephemeral private key produced by CreateSelfSigned.
                    return new X509Certificate2(cert.Export(X509ContentType.Pfx), (string)null, X509KeyStorageFlags.Exportable);
                }
            }
        }

        #endregion
    }
}
