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
    public class SslServerSecurity : TcpServerSecurity
    {
        private CertificateSource _certSrc;

        public SslServerSecurity(X509Certificate2 serverCertificate)
            : this(new CertificateSource.File(serverCertificate ?? throw new ArgumentNullException(nameof(serverCertificate))))
        {
        }

        public SslServerSecurity(CertificateSource certSrc)
        {
            _certSrc = certSrc ?? throw new ArgumentNullException(nameof(certSrc));
        }

        public SslProtocols Protocols { get; set; } = SslProtocols.None;

        /// <summary>
        /// Enables mutual TLS: the server requests a certificate from the client during the handshake.
        /// Default value: false (a client certificate is not requested; a certificate configured on the
        /// client side is never transmitted).
        /// </summary>
        public bool RequireClientCertificate { get; set; }

        /// <summary>
        /// Custom validation of the client certificate (e.g. against a thumbprint whitelist).
        /// The callback may be invoked with a null certificate (SslPolicyErrors.RemoteCertificateNotAvailable)
        /// when the client does not present one, even if RequireClientCertificate is true - it must return
        /// false in that case to reject such connections.
        /// Default value: null (default SslStream validation is used).
        /// </summary>
        public RemoteCertificateValidationCallback ClientCertificateValidator { get; set; }

        internal override string Name => "SSL";

#if NET5_0_OR_GREATER
        internal async override ValueTask<ByteTransport> SecureTransport(SocketTransport unsecureTransport, Endpoint endpoint, string channelId, IRpcLogger logger, CancellationToken cToken)
#else
        internal async override Task<ByteTransport> SecureTransport(SocketTransport unsecureTransport, Endpoint endpoint, string channelId, IRpcLogger logger, CancellationToken cToken)
#endif
        {
            var netStream = new NetworkStream(unsecureTransport.Socket, true);
            var sslStream = new SslStream(netStream, false, ClientCertificateValidator, null);

            try
            {
#if NET5_0_OR_GREATER
                var sslOptions = new SslServerAuthenticationOptions();
                sslOptions.ServerCertificateSelectionCallback = (sender, host) => _certSrc.GetCertificate(host);
                sslOptions.ClientCertificateRequired = RequireClientCertificate;
                sslOptions.EnabledSslProtocols = Protocols;

                await sslStream.AuthenticateAsServerAsync(sslOptions, cToken).ConfigureAwait(false);
#else
                await sslStream.AuthenticateAsServerAsync(_certSrc.GetCertificate(), RequireClientCertificate, Protocols, false).WithCancelToken(cToken).ConfigureAwait(false);
#endif
            }
            catch (Exception ex)
            {
                // The socket must be disposed before the SslStream: it aborts any receive the
                // SslStream still has pending, so disposing the stream cannot trigger the
                // AccessViolationException described in SslTransport.DisposeInternal().
                // Disposing the stream also releases its pooled buffers, which would otherwise
                // be orphaned on every failed handshake.
                try { unsecureTransport.Socket.Dispose(); } catch { }
                try { sslStream.Dispose(); } catch { }

                if (ex is AuthenticationException aex)
                    throw new RpcException(aex.Message, RpcRetCode.InvalidCredentials, aex);

                throw;
            }

            return new SslTransport(sslStream, unsecureTransport.Socket, channelId, logger);
        }
    }
}
