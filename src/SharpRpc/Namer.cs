// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    internal static class Namer
    {
        private static Dictionary<string, int> _seedByName = new Dictionary<string, int>();

        public static string GetInstanceName(Type classInfo)
        {
            return GetInstanceName(classInfo.Name);
        }

        public static string GetInstanceName(string className)
        {
            var instanceNo = 0;

            lock (_seedByName)
            {
                _seedByName.TryGetValue(className, out instanceNo);
                instanceNo++;
                _seedByName[className] = instanceNo;
            }

            return className + "[" + instanceNo + "]";
        }
    }
}
