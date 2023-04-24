// Copyright © 2022 Soft-Fx. All rights reserved.
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

namespace SharpRpc.Lib
{
    public delegate Task AsyncEventHandler<TArgs>(object sender, TArgs args)
        where TArgs : EventArgs;

    public static class AsyncEventHandler
    {
        public static Task InvokeAsync<TArgs>(this Func<object, TArgs, Task> func, object sender, TArgs e)
            where TArgs : EventArgs
        {
            return func == null ? Task.CompletedTask
                : Task.WhenAll(func.GetInvocationList().Select(d => ((Func<object, TArgs, Task>)d)(sender, e)));
        }

        public static Task InvokeAsync<TArgs>(this AsyncEventHandler<TArgs> func, object sender, TArgs e)
            where TArgs : EventArgs
        {
            return func == null ? Task.CompletedTask
                : Task.WhenAll(func.GetInvocationList().Select(d => ((AsyncEventHandler<TArgs>)d)(sender, e)));
        }
    }
}
