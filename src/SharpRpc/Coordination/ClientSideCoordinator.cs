using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal class ClientSideCoordinator : SessionCoordinator
    {
        private object _lockObj = new object();
        private States _state = States.PendingLogin;
        private TaskCompletionSource<ILoginMessage> _loginWaitHandle;
        private bool _isLogoutEnabled;
        private ClientCredentials _creds;

        public ClientSideCoordinator(bool isLogoutRequired)
        {
            _isLogoutEnabled = isLogoutRequired;
        }

        public override TimeSpan LoginTimeout => TimeSpan.FromSeconds(5);

        protected override void OnInit()
        {
            var clientEndpoint = (ClientEndpoint)Channel.Endpoint;
            _creds = clientEndpoint.Credentials;
            Channel.Dispatcher.Start();
        }

        public override async ValueTask<RpcResult> OnConnect(CancellationToken cToken)
        {
            lock (_lockObj)
            {
                _state = States.LoginInProgress;
                _loginWaitHandle = new TaskCompletionSource<ILoginMessage>();
            }

            // send login
            var loginMsg = Channel.Contract.SystemMessages.CreateLoginMessage();
            _creds.OnBeforeLogin(loginMsg);

            var sendResult = await Channel.Tx.SendSystemMessage(loginMsg);

            if (!sendResult.IsOk)
                return sendResult;

            using (cToken.Register(OnLoginTimeout))
            {
                // wait for response login (with timeout)
                var loginResp = await _loginWaitHandle.Task;

                if (loginResp == null)
                    return new RpcResult(RpcRetCode.LoginTimeout, "Login oepration timed out!");

                if (loginResp.ResultCode == LoginResult.Ok)
                    return RpcResult.Ok;
                else if (loginResp.ResultCode == LoginResult.InvalidCredentials)
                    return new RpcResult(RpcRetCode.InvalidCredentials, "Login failed: " + loginResp.ErrorMessage);
                else
                    return new RpcResult(RpcRetCode.ProtocolViolation, "Login failed: Invalid or missing result code in login response!");
            }
        }

        public override async ValueTask<RpcResult> OnDisconnect()
        {
            if (_isLogoutEnabled)
            {
                var logoutMsg = Channel.Contract.SystemMessages.CreateLogoutMessage();

                return await Channel.Tx.SendSystemMessage(logoutMsg);
            }

            return RpcResult.Ok;
        }

        public override RpcResult OnMessage(ISystemMessage message)
        {
            lock (_lockObj)
            {
                if (message is ILoginMessage loginMsg)
                {
                    if (_state != States.LoginInProgress)
                        return new RpcResult(RpcRetCode.ProtocolViolation, "Unexpected login message!");

                    _state = States.LoggedIn;
                    _loginWaitHandle.TrySetResult(loginMsg);
                }
            }

            return RpcResult.Ok;
        }

        private void OnLoginTimeout()
        {
            _loginWaitHandle.TrySetResult(null);
        }
    }
}
