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
    public interface IMessageFactory
    {
        ILoginMessage CreateLoginMessage();
        ILogoutMessage CreateLogoutMessage();
        IHeartbeatMessage CreateHeartBeatMessage();
        ICancelRequestMessage CreateCancelRequestMessage();
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
    }

    public interface IHeartbeatMessage : ISystemMessage
    {
    }

    public enum LoginResult
    {
        Ok,
        InvalidCredentials
    }

    public enum RequestFaultCode
    {
        Fault,
        Crash
    }
}
