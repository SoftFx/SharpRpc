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
    public interface PasswordValidator
    {
        /// <summary>
        /// Implement this method to provide basic login/password authentication.
        /// </summary>
        /// <param name="userName">User name to validate.</param>
        /// <param name="password">Password to validate.</param>
        /// <returns>Returns null if password is valid, otherwise error message.</returns>
#if NET5_0_OR_GREATER
        ValueTask<string> Validate(string userName, string password);
#else
        Task<string> Validate(string userName, string password);
#endif
    }
}
