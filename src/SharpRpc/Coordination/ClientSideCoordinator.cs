using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal class ClientSideCoordinator : SessionCoordinator
    {
        private object _lockObj = new object();
        private States _state = States.PendingLogin;
        private TaskCompletionSource<RpcResult> _loginWaitHandle;
        //private TaskCompletionSource<ILogoutMessage> _logoutWaitHandle;
        private bool _isLogoutEnabled;

        public ClientSideCoordinator(bool isLogoutRequired)
        {
            _isLogoutEnabled = isLogoutRequired;
        }

        public override async ValueTask<RpcResult> OnConnect()
        {
            lock (_lockObj)
            {
                _state = States.LoginInProgress;
                _loginWaitHandle = new TaskCompletionSource<RpcResult>();
            }

            // send login
            var loginMsg = Channel.Contract.SystemMessages.CreateLoginMessage();
            var sendResult = await Channel.Tx.SendSystemMessage(loginMsg);

            if (!sendResult.IsOk)
                return sendResult;

            // wait for response login (with timeout)
            return await _loginWaitHandle.Task;
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

        public override RpcResult OnMessage(ISystemMessage message, out bool isLoggedIn)
        {
            isLoggedIn = false;

            lock (_lockObj)
            {
                if (message is ILoginMessage loginMsg)
                {
                    if (_state != States.LoginInProgress)
                        return new RpcResult(RpcRetCode.ProtocolViolation, "Unexpected login message!");

                    isLoggedIn = true;
                    _state = States.LoggedIn;
                    _loginWaitHandle.TrySetResult(RpcResult.Ok);
                }
            }

            return RpcResult.Ok;
        }
    }
}
