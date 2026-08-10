// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class SslSecurity : TcpSecurity
    {
        private readonly RemoteCertificateValidationCallback _customCertValidator;

        public SslSecurity(RemoteCertificateValidationCallback serverCertValidator = null)
        {
            _customCertValidator = serverCertValidator;
        }

        public SslProtocols Protocols { get; set; } = SslProtocols.None;
        public bool EnableRevocationCheck { get; set; } = true;

        /// <summary>
        /// Certificates to present to the server for mutual TLS. When the server requests a certificate,
        /// the first one whose issuer matches the server's acceptable-issuers list is presented;
        /// if none matches (e.g. self-signed certificates), the first certificate is presented.
        /// If the server does not request a client certificate, nothing is transmitted.
        /// Default value: null (no client certificate is sent).
        /// </summary>
        public X509CertificateCollection ClientCertificates { get; set; }

#if NET5_0_OR_GREATER
        internal async override ValueTask<ByteTransport> SecureTransport(Socket socket, Endpoint endpoint, string targetHost, string channelId, IRpcLogger logger, CancellationToken cToken)
#else
        internal async override Task<ByteTransport> SecureTransport(Socket socket, Endpoint endpoint, string targetHost, string channelId, IRpcLogger logger, CancellationToken cToken)
#endif
        {
            var clientCerts = ClientCertificates;
            var clientCertSelector = clientCerts != null && clientCerts.Count > 0
                ? new LocalCertificateSelectionCallback(SelectClientCertificate)
                : null;

            var netStream = new NetworkStream(socket, true);
            var sslStream = new SslStream(netStream, false, _customCertValidator, clientCertSelector, EncryptionPolicy.RequireEncryption);

            try
            {
#if NET5_0_OR_GREATER
                var sslOptions = new SslClientAuthenticationOptions()
                {
                    TargetHost = targetHost,
                    ClientCertificates = clientCerts,
                    EnabledSslProtocols = Protocols,
                    CertificateRevocationCheckMode = EnableRevocationCheck ? X509RevocationMode.Online : X509RevocationMode.NoCheck,
                    EncryptionPolicy = EncryptionPolicy.RequireEncryption,
                };

                await sslStream.AuthenticateAsClientAsync(sslOptions, cToken).ConfigureAwait(false);
#else
                await sslStream.AuthenticateAsClientAsync(targetHost, clientCerts, Protocols, EnableRevocationCheck).WithCancelToken(cToken).ConfigureAwait(false);
#endif
            }
            catch (Exception ex)
            {
                // The socket must be disposed before the SslStream: it aborts any receive the
                // SslStream still has pending, so disposing the stream cannot trigger the
                // AccessViolationException described in SslTransport.DisposeInternal().
                // Disposing the stream also releases its pooled buffers, which would otherwise
                // be orphaned on every failed handshake.
                try { socket.Dispose(); } catch { }
                try { sslStream.Dispose(); } catch { }

                if (ex is AuthenticationException aex)
                    throw new RpcException(aex.Message, RpcRetCode.InvalidCredentials, aex);

                throw;
            }

            return new SslTransport(sslStream, socket, channelId, logger);
        }

        internal static X509Certificate SelectClientCertificate(object sender, string targetHost, X509CertificateCollection localCertificates,
            X509Certificate remoteCertificate, string[] acceptableIssuers)
        {
            if (localCertificates == null || localCertificates.Count == 0)
                return null;

            if (acceptableIssuers != null && acceptableIssuers.Length > 0)
            {
                foreach (X509Certificate cert in localCertificates)
                {
                    if (Array.IndexOf(acceptableIssuers, cert.Issuer) >= 0)
                        return cert;
                }
            }

            // No issuer match - present the first certificate anyway. The default selection logic
            // would send nothing here, which silently rejects self-signed certificates.
            return localCertificates[0];
        }
    }
}
