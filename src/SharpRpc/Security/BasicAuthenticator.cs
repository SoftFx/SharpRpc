// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class BasicAuthenticator : Authenticator
    {
        private PasswordValidator _validator;

        public BasicAuthenticator(PasswordValidator validator)
        {
            _validator = validator ?? throw new ArgumentNullException("validator");
        }

#if NET5_0_OR_GREATER
        internal override ValueTask<string> OnLogin(ILoginMessage login, SessionContext context)
#else
        internal override Task<string> OnLogin(ILoginMessage login, SessionContext context)
#endif
        {
            if (string.IsNullOrEmpty(login.UserName))
            {
                var msg = "UserName field is empty!";
#if NET5_0_OR_GREATER
                return ValueTask.FromResult(msg);
#else
                return Task.FromResult(msg);
#endif
            }

            if (login.Password == null)
            {
                var msg = "Password field is empty!";
#if NET5_0_OR_GREATER
                return ValueTask.FromResult(msg);
#else
                return Task.FromResult(msg);
#endif
            }

            return _validator.Validate(login.UserName, login.Password, context);
        }
    }
}
