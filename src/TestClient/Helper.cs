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

namespace TestClient
{
    public static class Helper
    {
        public static T Pick<T>(this Random rnd, params T[] pickItems)
        {
            var index = rnd.Next(0, pickItems.Length);
            return pickItems[index];
        }
    }
}
