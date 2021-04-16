using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal class ServerSideCoordinator : SessionCoordinator
    {
        private object _lockObj = new object();
        private States _state = States.PendingLogin;
        private TaskCompletionSource<RpcResult> _loginWaitHandle = new TaskCompletionSource<RpcResult>();

        public override ValueTask<RpcResult> OnConnect()
        {
            // wait for login (with timeout)
            // check login/password
            // send login response
            return new ValueTask<RpcResult>(_loginWaitHandle.Task);
        }

        public override ValueTask<RpcResult> OnDisconnect()
        {
            return ValueTask.FromResult(RpcResult.Ok);
        }

        public override RpcResult OnMessage(ISystemMessage message, out bool isLoggedIn)
        {
            isLoggedIn = false;

            lock (_lockObj)
            {
                if (message is ILoginMessage loginMsg)
                {
                    if (_state != States.PendingLogin)
                        return new RpcResult(RpcRetCode.ProtocolViolation, "Unexpected login message!");

                    isLoggedIn = true;
                    _state = States.LoggedIn;
                    _loginWaitHandle.TrySetResult(RpcResult.Ok);

                    SendLoginResponse();
                }
                else if (message is ILogoutMessage logoutMsg)
                {
                }
            }

            return RpcResult.Ok;
        }

        private void SendLoginResponse()
        {
            var loginMsg = Channel.Contract.SystemMessages.CreateLoginMessage();
            Channel.Tx.SendSystemMessage(loginMsg);
        }
    }
}
