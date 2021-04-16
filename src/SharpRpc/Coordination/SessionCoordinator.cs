using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal abstract class SessionCoordinator
    {
        protected Channel Channel { get; private set; }

        public void Init(Channel ch)
        {
            Channel = ch;
        }

        public abstract RpcResult OnMessage(ISystemMessage message, out bool isLoggedIn);
        public abstract ValueTask<RpcResult> OnConnect();
        public abstract ValueTask<RpcResult> OnDisconnect();

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
