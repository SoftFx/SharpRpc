using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class RpcServer
    {
        private enum ServerState { Idle, Starting, Online, Stopping  }

        private readonly object _stateLock = new object();
        private readonly List<ServerEndpoint> _endpoints = new List<ServerEndpoint>();
        private ServerState _state;
        private readonly Dictionary<Guid, RpcSession> _sessions = new Dictionary<Guid, RpcSession>();
        private readonly ServiceBinding _binding;

        public RpcServer(ServiceBinding serviceBinding)
        {
            Name = Namer.GetInstanceName(GetType());

            _binding = serviceBinding;
        }

        public RpcServer AddEndpoint(ServerEndpoint endpoint)
        {
            lock (_stateLock)
            {
                endpoint.Init(this);
                endpoint.ClientConnected += Endpoint_ClientConnected;
                _endpoints.Add(endpoint);
            }

            return this;
        }

        internal LoggerFacade Logger { get; } = new LoggerFacade();

        public string Name { get; }

        public RpcServer SetLogger(IRpcLogger logger)
        {
            lock (_stateLock)
            {
                ThrowIfConfigProhibited();
                Logger.SetExtLogger(logger);
            }

            return this;
        }

        public void Start()
        {
            lock (_stateLock)
            {
                if (_state != ServerState.Idle)
                    throw new InvalidOperationException("Start is not possible at this time! State: " + _state);

                _state = ServerState.Starting;
            }

            Logger.Info(Name, "Starting...");

            try
            {
                foreach (var endpoint in _endpoints)
                    endpoint.Validate();

                foreach (var endpoint in _endpoints)
                    endpoint.InvokeStart();

                Logger.Info(Name, "Started.");

                lock (_stateLock)
                    _state = ServerState.Online;
            }
            catch (Exception ex)
            {
                lock (_stateLock)
                    _state = ServerState.Idle;

                // TO DO: stop started endpoints
                throw new Exception("Failed to start RPC server! " + ex.Message);
            }
            //finally
            //{
            //    lock (_stateLock)
            //        _state = ServerState.Online;
            //}
        }

        public async Task StopAsync()
        {
            lock (_stateLock)
            {
                if (_state != ServerState.Online)
                    throw new InvalidOperationException("Stop is not possible at this time! State: " + _state);

                _state = ServerState.Starting;
            }

            Logger.Info(Name, "Stopping...");

            var stopTasks = _endpoints.Select(e => e.InvokeStop());

            await Task.WhenAll(stopTasks.ToList());

            Logger.Info(Name, "Stopped.");

            lock (_stateLock)
                _state = ServerState.Idle;
        }

        private void Endpoint_ClientConnected(ServerEndpoint sender, ByteTransport channel)
        {
            var session = new RpcSession(channel, _binding, sender);

            //_handlers.Add(handler.Id, handler);

            Logger.Verbose(Name, "New session: " + session.Id);
        }

        private void ThrowIfConfigProhibited()
        {
            if (_state != ServerState.Idle)
                throw new InvalidOperationException("Changing configuration in runtime is prohibited!");
        }
    }
}
