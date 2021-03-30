using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public interface ISystemMessageFactory
    {
        ILoginMessage CreateLoginMessage();
        ILogoutMessage CreateLogoutMessage();
        IHeartbeatMessage CreateHeartBeatMessage();
    }

    public interface ILoginMessage : IMessage
    {
    }

    public interface ILogoutMessage : IMessage
    {
    }

    public interface IHeartbeatMessage : IMessage
    {
    }
}
