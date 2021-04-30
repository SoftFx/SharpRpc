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
    public class BasicAuthenticator : Authenticator
    {
        private PasswordValidator _validator;

        public BasicAuthenticator(PasswordValidator validator)
        {
            _validator = validator ?? throw new ArgumentNullException("validator");
        }

        internal override ValueTask<string> OnLogin(ILoginMessage login)
        {
            if (string.IsNullOrEmpty(login.UserName))
                return ValueTask.FromResult("UserName field is empty!");

            if (login.Password == null)
                return ValueTask.FromResult("Password field is empty!");

            return _validator.Validate(login.UserName, login.Password);
        }
    }
}
