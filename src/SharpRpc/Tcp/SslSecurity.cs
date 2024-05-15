// Copyright © 2021 Soft-Fx. All rights reserved.
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
    public class SslSecurity : TcpSecurity
    {
        private readonly RemoteCertificateValidationCallback _customCertValidator;

        public SslSecurity(RemoteCertificateValidationCallback serverCertValidator = null)
        {
            _customCertValidator = serverCertValidator;
        }

        public SslProtocols Protocols { get; set; } = SslProtocols.None;
        public bool EnableRevocationCheck { get; set; } = true;

#if NET5_0_OR_GREATER
        internal async override ValueTask<ByteTransport> SecureTransport(Socket socket, Endpoint endpoint, string targetHost, string channelId, IRpcLogger logger)
#else
        internal async override Task<ByteTransport> SecureTransport(Socket socket, Endpoint endpoint, string targetHost, string channelId, IRpcLogger logger)
#endif
        {
            var netStream = new NetworkStream(socket, true);
            var sslStream = new SslStream(netStream, false, _customCertValidator, null, EncryptionPolicy.RequireEncryption);

            try
            {
                await sslStream.AuthenticateAsClientAsync(targetHost, null, Protocols, EnableRevocationCheck).ConfigureAwait(false);
            }
            catch (AuthenticationException aex)
            {
                throw new RpcException(aex.Message, RpcRetCode.InvalidCredentials);
            }

            return new SslTransport(sslStream, socket, channelId, logger);
        }
    }
}
