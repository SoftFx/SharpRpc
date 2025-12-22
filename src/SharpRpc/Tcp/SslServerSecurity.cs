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

        internal override string Name => "SSL";

#if NET5_0_OR_GREATER
        internal async override ValueTask<ByteTransport> SecureTransport(SocketTransport unsecureTransport, Endpoint endpoint, string channelId, IRpcLogger logger, CancellationToken cToken)
#else
        internal async override Task<ByteTransport> SecureTransport(SocketTransport unsecureTransport, Endpoint endpoint, string channelId, IRpcLogger logger, CancellationToken cToken)
#endif
        {
            var netStream = new NetworkStream(unsecureTransport.Socket, true);
            var sslStream = new SslStream(netStream, false);

#if NET5_0_OR_GREATER
            var sslOptions = new SslServerAuthenticationOptions();
            sslOptions.ServerCertificateSelectionCallback = (sender, host) => _certSrc.GetCertificate(host);
            sslOptions.ClientCertificateRequired = false;
            sslOptions.EnabledSslProtocols = Protocols;

            await sslStream.AuthenticateAsServerAsync(sslOptions, cToken).ConfigureAwait(false);
#else
            await sslStream.AuthenticateAsServerAsync(_certSrc.GetCertificate(), false, Protocols, false).WithCancelToken(cToken).ConfigureAwait(false);
#endif

            return new SslTransport(sslStream, unsecureTransport.Socket, channelId, logger);
        }
    }
}
