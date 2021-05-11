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

namespace TestServer
{
    internal class AuthValidator : SharpRpc.PasswordValidator
    {
        public ValueTask<string> Validate(string userName, string password)
        {
            var valid = userName == "Admin" && password == "zzzz";

            if (!valid)
                return ValueTask.FromResult("Invalid credentials.");
            
            return ValueTask.FromResult<string>(null);
        }
    }
}
