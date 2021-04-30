// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.Builder
{
    public class ParamDeclaration
    {
        public ParamDeclaration(int index, string type, string name = null)
        {
            Index = index;
            ParamType = type;
            ParamName = name;
            MessagePropertyName = "Arg" + index;
        }

        public string ParamType { get; }
        public string ParamName { get; }
        public int Index { get; }
        public string MessagePropertyName { get; }
    }
}
