using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SharpRpc
{
    internal interface CertificateSource
    {
        X509Certificate2 GetCertificate();

        public class File : CertificateSource
        {
            private readonly X509Certificate2 _cert;

            public File(X509Certificate2 cert)
            {
                _cert = cert;
            }

            public X509Certificate2 GetCertificate()
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
