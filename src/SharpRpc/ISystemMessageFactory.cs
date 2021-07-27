// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

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
        IRequestFault<T> CreateFaultMessage<T>(T fault) where T : RpcFault;
        IRequestFault CreateFaultMessage();
        //IBasicAuthData CreateBasicAuthData();
    }

    public interface ILoginMessage : ISystemMessage
    {
        string UserName { get; set; }
        string Password { get; set; }
        LoginResult? ResultCode { get; set; }
        string ErrorMessage { get; set; }
    }

    public interface ILogoutMessage : ISystemMessage
    {
        //LogoutOption Mode { get; set; }
    }

    public interface IHeartbeatMessage : ISystemMessage
    {
    }

    public interface IRequestFault : IResponse
    {
        string Text { get; set; }
        RequestFaultCode Code { get; set; }
        RpcFaultException CreateException();
        RpcFault GetFault();
    }

    public interface IRequestFault<T> : IRequestFault
        where T : RpcFault
    {
        T FaultData { get; set; }
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

    public enum RequestFaultCode
    {
        CustomFault,
        RegularFault,
        UnexpectedFault
    }
}
