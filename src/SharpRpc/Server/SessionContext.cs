// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc.Server
{
    public class SessionContext
    {
        internal SessionContext(string sessionId)
        {
            SessionId = sessionId;
        }

        public string SessionId { get; }

        public CustomProperties Properties { get; } = new CustomProperties();
    }

    public class CustomProperties
    {
        private readonly ConcurrentDictionary<string, object> _properties = new ConcurrentDictionary<string, object>();

        public bool TryGetValue(string propertyName, out object value)
        {
            return _properties.TryGetValue(propertyName, out value);
        }

        public object this[string name]
        {
            get
            {
                _properties.TryGetValue(name, out var propertyVal);
                return propertyVal;
            }

            set
            {
                _properties[name] = value;
            }
        }
    }
}
