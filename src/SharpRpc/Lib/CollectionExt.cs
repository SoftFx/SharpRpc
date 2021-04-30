// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.Lib
{
    public static class CollectionExt
    {
        public static void DequeueRange<T>(this Queue<T> queue, List<T> toContainer, int maxItems)
        {
            while (toContainer.Count < maxItems)
            {
                if (queue.Count > 0)
                    toContainer.Add(queue.Dequeue());
                else
                    break;
            }
        }
    }
}
