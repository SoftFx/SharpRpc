using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class TcpClientEndpoint : ClientEndpoint
    {
        private string _address;
        private int _port;

        public TcpClientEndpoint(string address, int port)
        {
            _address = address;
            _port = port;
        }

        public override async Task<ByteTransport> ConnectAsync()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(_address);
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint remoteEP = new IPEndPoint(ipAddress, _port);

            // Create a TCP/IP socket.  
            var socket = new Socket(ipAddress.AddressFamily,
                SocketType.Stream, ProtocolType.Tcp);

            await socket.ConnectAsync(remoteEP);

            return new TcpTransport(socket);
        }
    }
}
