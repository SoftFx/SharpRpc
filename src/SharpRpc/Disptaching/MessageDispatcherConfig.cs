// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class MessageDispatcherConfig : ConfigElement
    {
        //private DispatcherConcurrencyMode _rxConcurrency = DispatcherConcurrencyMode.None;

        internal MessageDispatcherConfig()
        {
        }

        //public DispatcherConcurrencyMode RxConcurrencyMode
        //{
        //    get => _rxConcurrency;
        //    set
        //    {
        //        lock (Host.SyncObject)
        //        {
        //            ThrowIfImmutable();
        //            _rxConcurrency = value;
        //        }
        //    }
        //}
    }

    //public enum DispatcherConcurrencyMode
    //{
    //    /// <summary>
    //    /// No concurrency. In this mode the dispatcher calls service methods in the same thread it recives them.
    //    /// Pros: Lower CPU usage. Cons: Low throughput.
    //    /// This mode is usefull if all call handling is delegated to other threads/actors or call handling is really lightweight and takes no time.
    //    /// </summary>
    //    None,

    //    /// <summary>
    //    /// All call handlers are invoked in single dedicated thread. Default mode.
    //    /// </summary>
    //    Single,

    //    /// <summary>
    //    /// 
    //    /// </summary>
    //    //Multiple
    //}
}
