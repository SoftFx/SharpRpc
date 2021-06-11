// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SharpRpc
{
    public abstract class CertificateSource
    {
        public abstract X509Certificate2 GetCertificate();

        public class File : CertificateSource
        {
            private readonly X509Certificate2 _cert;

            public File(X509Certificate2 cert)
            {
                _cert = cert;
            }

            public override X509Certificate2 GetCertificate()
            {
                return _cert;
            }
        }
    }

    public class CertLoadError : Exception
    {
        public CertLoadError(string message) : base("Failed to load a certificate: " + message)
        {
        }

        public CertLoadError(Exception innerEx) : base("Failed to load a certificate: " + innerEx.Message, innerEx)
        {
        }
    }
}
