﻿// Copyright © 2021 Soft-Fx. All rights reserved.
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
    public class TypeString
    {
        public TypeString(string typeFullName)
        {
            Full = typeFullName.Trim();

            var nsDelimiterIndex = Full.LastIndexOf(".");

            if (nsDelimiterIndex == 0 || nsDelimiterIndex >= Full.Length - 1)
                throw new Exception();

            if (nsDelimiterIndex > 0)
            {
                Namespace = Full.Substring(0, nsDelimiterIndex);
                Short = Full.Substring(nsDelimiterIndex + 1);
            }
            else
            {
                Namespace = "";
                Short = typeFullName;
            }
        }

        public TypeString(string ns, string name)
        {
            Namespace = ns.Trim();
            Short = name.Trim();
            Full = ns + "." + name;
        }

        public string Namespace { get; }
        public string Short { get; }
        public string Full { get; }
    }
}
