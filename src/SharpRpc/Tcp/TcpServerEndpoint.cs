using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        private readonly IPEndPoint _ipEndpoint;
        private readonly TcpServerSecurity _security;

        public TcpServerEndpoint(IPEndPoint ipEndpoint, TcpServerSecurity security)
        {
            _security = security ?? throw new ArgumentNullException("security");
            _ipEndpoint = ipEndpoint;

            _listener = new Socket(_ipEndpoint.Address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        public TcpServerEndpoint(IPAddress address, int port, TcpServerSecurity security)
            : this(new IPEndPoint(address, port), security)
        {
        }

        public TcpServerEndpoint(string address, TcpServerSecurity security)
        {
            var addressParts = address.Split(':');

            if (addressParts.Length != 2)
                throw new ArgumentException("Invalid address format. Please provide address in host:port format.");

            if (!int.TryParse(addressParts[1].Trim(), out int port) || port < 0)
                throw new ArgumentException("Invalid port. Port must be a positive integer.");

            IPHostEntry ipHostInfo = Dns.GetHostEntry(addressParts[0].Trim());
            var ipAddress = ipHostInfo.AddressList[0];
            _ipEndpoint = new IPEndPoint(ipAddress, port);

            _listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        }

        protected LoggerFacade Logger { get; private set; }

        protected override void Start(LoggerFacade logger)
        {
            Logger = logger;
            _stopFlag = false;

            _security.Init();

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

        protected virtual ValueTask<ByteTransport> GetTransport(Socket socket)
        {
            return new ValueTask<ByteTransport>(new TcpTransport(socket));
        }

        private async Task AcceptLoop()
        {
            while (!_stopFlag)
            {
                Socket socket;

                try
                {
                    socket = await _listener.AcceptAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    var socketEx = ex as SocketException;

                    if (!_stopFlag || socketEx == null || socketEx.SocketErrorCode != SocketError.OperationAborted)
                        Logger.Error(Name, ex.Message);

                    continue;
                }

                try
                {
                    var transport = await _security.SecureTransport(socket);

                    OnConnect(transport);
                }
                catch (Exception ex)
                {
                    //var socketEx = ex as SocketException;
                    Logger.Error(Name, ex.Message);

                    try
                    {
                        await socket.DisconnectAsync();
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
