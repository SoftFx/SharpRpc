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
    public static class ExceptionHelper
    {
        public static string JoinAllMessages(this Exception ex)
        {
            var builder = new StringBuilder();

            while (ex != null)
            {
                builder.Append(ex.Message);
                ex = ex.InnerException;
            }

            return builder.ToString();
        }
    }
}
