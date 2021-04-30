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
    public class BasicCredentials : Credentials
    {
        private string _userName;
        private string _password;

        public BasicCredentials(string userName, string password)
        {
            _userName = userName ?? throw new ArgumentNullException(nameof(userName));
            _password = password ?? throw new ArgumentNullException(nameof(password));

            if (string.IsNullOrEmpty(userName))
                throw new ArgumentException("User name cannot be empty string!");
        }

        internal override void OnBeforeLogin(ILoginMessage loginMsg)
        {
            loginMsg.Password = _password;
            loginMsg.UserName = _userName;
        }
    }
}
