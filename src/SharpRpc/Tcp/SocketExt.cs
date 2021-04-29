using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal static class SocketExt
    {
        public static Task DisconnectAsync(this Socket socket)
        {
            return Task.Factory.FromAsync((c, s) => socket.BeginDisconnect(false, c, s),
                    r => socket.EndDisconnect(r), null);
        }
    }
}
