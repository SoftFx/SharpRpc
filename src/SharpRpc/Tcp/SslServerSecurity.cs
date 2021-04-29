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

        internal override void Init()
        {
            _cert = LoadCertificate();
        }

        internal async override ValueTask<ByteTransport> SecureTransport(Socket socket)
        {
            var netStream = new NetworkStream(socket);
            var sslStream = new SslStream(netStream);

            var sslOptions = new SslServerAuthenticationOptions();
            sslOptions.ServerCertificate = _cert;
            sslOptions.ClientCertificateRequired = false;
            sslOptions.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;

            await sslStream.AuthenticateAsServerAsync(sslOptions);

            return new SslTransport(sslStream);
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
