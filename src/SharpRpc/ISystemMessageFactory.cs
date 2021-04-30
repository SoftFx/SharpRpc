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
