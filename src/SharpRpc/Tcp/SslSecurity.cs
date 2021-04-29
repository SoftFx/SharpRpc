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
        private readonly CertificateSource _serverCertSrc;

        public SslSecurity()
        {
        }

        public SslSecurity(X509Certificate2 serverCertificate)
        {
            _serverCertSrc = new CertificateSource.File(serverCertificate);
        }

        public SslSecurity(StoredCertificate serverCertificate)
        {
            _serverCertSrc = serverCertificate;
        }

        internal async override ValueTask<ByteTransport> SecureTransport(Socket socket, string targetHost)
        {
            var stream = new NetworkStream(socket, true);
            var sslStream = new SslStream(stream);

            //var cert = _certSrc.GetCertificate();
            var options = new SslClientAuthenticationOptions();
            options.EncryptionPolicy = EncryptionPolicy.RequireEncryption;
            options.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
            options.TargetHost = targetHost;

            //if (_serverCertSrc != null)
            //{
            //    options.LocalCertificateSelectionCallback = ProvideSuppliedCertificate;
            //}

            //if (_certSrc != null)
            //{
            //    options.ClientCertificates = new X509CertificateCollection();
            //    options.ClientCertificates.Add(cert);
            //}

            options.TargetHost = targetHost;
            //options.RemoteCertificateValidationCallback = ValidateServerCertificate;

            try
            {
                await sslStream.AuthenticateAsClientAsync(options);
            }
            catch (AuthenticationException aex)
            {
                throw new RpcException(aex.Message, RpcRetCode.InvalidCredentials);
            }

            return new SslTransport(sslStream);
        }

        //private bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        //{
        //    return true;
        //}

        //private X509Certificate ProvideSuppliedCertificate(object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
        //{
        //    return _serverCertSrc.GetCertificate();
        //}
    }
}
