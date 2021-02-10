using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class ServerEndpoint : Endpoint
    {
        //private readonly object _stateLockObj = new object();
        private RpcServer _server;

        public ServerEndpoint()
        {
            Name = Namer.GetInstanceName(GetType());
        }

        public string Name { get; }

        internal void Init(RpcServer server)
        {
            lock (_stateLockObj)
            {
                if (_server != null)
                    throw new InvalidOperationException("This endpoint belongs to other server!");

                _server = server;
            }
        }

        protected abstract void Start(LoggerFacade logger);
        protected abstract Task StopAsync(LoggerFacade logger);

        protected void OnConnect(ByteTransport newConnection)
        {
            _server.Logger.Verbose(Name, "Incoming connection");

            ClientConnected.Invoke(this, newConnection);
        }

        internal event Action<ServerEndpoint, ByteTransport> ClientConnected;

        internal void InvokeStart()
        {
            _server.Logger.Info(Name, "Starting...");

            Start(_server.Logger);

            _server.Logger.Info(Name, "Started.");
        }

        internal async Task InvokeStop()
        {
            _server.Logger.Info(Name, "Stopping...");

            try
            {
                await StopAsync(_server.Logger);
            }
            catch (Exception ex)
            {
                _server.Logger.Error(Name, ex, "Stop failed! " + ex.Message);
            }

            _server.Logger.Info(Name, "Stopping...");
        }
    }
}
