using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class TcpServerEndpoint : ServerEndpoint
    {
        //private readonly object _lockObj;
        private readonly Socket _listener;
        private Task _listenerTask;
        private volatile bool _stopFlag;
        private LoggerFacade _logger;
        private readonly IPEndPoint _ipEndpoint;

        public TcpServerEndpoint(int port)
        {
            ///IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = IPAddress.IPv6Loopback;// ipHostInfo.AddressList[0];
            _ipEndpoint = new IPEndPoint(ipAddress, port);

            _listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        protected override void Start(LoggerFacade logger)
        {
            _logger = logger;
            _stopFlag = false;

            _listener.Bind(_ipEndpoint);
            _listener.Listen(100);

            _listenerTask = AcceptLoop();
        }

        protected override async Task StopAsync(LoggerFacade logger)
        {
            _stopFlag = true;

            _listener.Close();

            await _listenerTask;
        }

        private async Task AcceptLoop()
        {
            try
            {
                while (!_stopFlag)
                {
                    var socket = await _listener.AcceptAsync().ConfigureAwait(false);
                    OnConnect(new TcpTransport(socket));
                }
            }
            catch (Exception ex)
            {
                var socketEx = ex as SocketException;

                if (!_stopFlag || socketEx == null || socketEx.SocketErrorCode != SocketError.OperationAborted)
                    _logger.Error(Name, ex.Message);
            }
        }
    }
}
