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
        //IBasicAuthData CreateBasicAuthData();
    }

    public interface ILoginMessage : ISystemMessage
    {
        public string UserName { get; set; }
        public string Password { get; set; }
        public LoginResult? ResultCode { get; set; }
        public string ErrorMessage { get; set; }
    }

    public interface ILogoutMessage : ISystemMessage
    {
    }

    public interface IHeartbeatMessage : ISystemMessage
    {
    }

    //public interface IAuthData
    //{
    //}

    //public interface IBasicAuthData : IAuthData
    //{
    //    public string UserName { get; set; }
    //    public string Password { get; set; }
    //}

    public enum LoginResult
    {
        Ok,
        InvalidCredentials
    }
}
