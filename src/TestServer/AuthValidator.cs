// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc;
using SharpRpc.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestServer
{
    internal class AuthValidator : PasswordValidator
    {
#if NET5_0_OR_GREATER
        public ValueTask<string> Validate(string userName, string password, SessionContext context)
#else
        public Task<string> Validate(string userName, string password, SessionContext context)
#endif
        {
            var valid = userName == "Admin" && password == "zzzz";

            if (!valid)
                return FwAdapter.WrappResult("Invalid credentials.");

            context.Properties["UserName"] = userName;
            context.Properties["AuthToken"] = 15;

            return FwAdapter.WrappResult((string)null);
        }
    }
}
