using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal abstract class SessionCoordinator
    {
        protected Channel Channel { get; private set; }

        public void Init(Channel ch)
        {
            Channel = ch;
            OnInit();
        }

        public abstract TimeSpan LoginTimeout { get; }

        public abstract RpcResult OnMessage(ISystemMessage message);
        public abstract ValueTask<RpcResult> OnConnect(CancellationToken cToken);
        public abstract ValueTask<RpcResult> OnDisconnect();

        protected virtual void OnInit() { }

        public enum States
        {
            PendingLogin,
            LoginInProgress,
            LoggedIn,
            LogoutInProgress,
            LoggedOut
        }
    }
}
