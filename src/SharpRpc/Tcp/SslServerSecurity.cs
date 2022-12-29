﻿// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class SslServerSecurity : TcpServerSecurity
    {
        private CertificateSource _certSrc;
        private X509Certificate2 _cert;

        public SslServerSecurity(X509Certificate2 serverCertificate)
        {
            _certSrc = new CertificateSource.File(serverCertificate ?? throw new ArgumentNullException(nameof(serverCertificate)));
        }

        public SslServerSecurity(StoredCertificate serverCertificate)
        {
            _certSrc = serverCertificate ?? throw new ArgumentNullException(nameof(serverCertificate));
        }

        internal override string Name => "SSL";

        internal override void Init()
        {
            _cert = LoadCertificate();
        }

#if NET5_0_OR_GREATER
        internal async override ValueTask<ByteTransport> SecureTransport(Socket socket, Endpoint endpoint)
#else
        internal async override Task<ByteTransport> SecureTransport(Socket socket, Endpoint endpoint)
#endif
        {
            var netStream = new NetworkStream(socket);
            var sslStream = new SslStream(netStream);

#if NET5_0_OR_GREATER
            var sslOptions = new SslServerAuthenticationOptions();
            sslOptions.ServerCertificate = _cert;
            sslOptions.ClientCertificateRequired = false;
            sslOptions.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;

            await sslStream.AuthenticateAsServerAsync(sslOptions);
#else
            await sslStream.AuthenticateAsServerAsync(_cert, false, SslProtocols.Tls11 | SslProtocols.Tls12, false);
#endif

            return new SslTransport(sslStream, socket);
        }

        private X509Certificate2 LoadCertificate()
        {
            try
            {
                return _certSrc.GetCertificate();
            }
            catch (CertLoadError)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to load certificate: " + ex.Message, ex);
            }
        }
    }
}
