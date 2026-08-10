// Copyright © 2026 Soft-Fx. All rights reserved.
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace SharpRpc.Tcp
{
    public class SslConnectionInfo : TcpConnectionInfo
    {
        internal SslConnectionInfo(IPEndPoint remEp, IPEndPoint localEp, X509Certificate2 remoteCertificate)
            : base(remEp, localEp)
        {
            RemoteCertificate = remoteCertificate;
        }

        /// <summary>
        /// The certificate presented by the remote side during the TLS handshake: the client certificate
        /// on the server side, the server certificate on the client side. Null if the remote side
        /// did not present one (e.g. mutual TLS is not enabled).
        /// </summary>
        public X509Certificate2 RemoteCertificate { get; }

        internal override void DumptTo(Log log)
        {
            base.DumptTo(log);

            if (log.InfoEnabled && RemoteCertificate != null)
                log.Info($"RemoteCertificate={RemoteCertificate.Thumbprint}");
        }
    }
}
