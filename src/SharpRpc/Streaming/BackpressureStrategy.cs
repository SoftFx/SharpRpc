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
    internal abstract class BackpressureStrategy
    {
        public abstract void OnItemsArrived(int count, int size);
        public abstract void OnItemsConsumed(int count, int size);

        internal class SizeLimit : BackpressureStrategy
        {
        }

        internal class ItemLimit : BackpressureStrategy
        {
        }
    }
}
