// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc.Config
{
    public class ConfigElement
    {
        private readonly List<ConfigElement> _children = new List<ConfigElement>();

        internal ConfigElement()
        {
        }

        protected object LockObject { get; } = new object();
        internal object Parent { get; private set; }
        internal bool IsLocked { get; private set; }

        internal void AttachTo(object parent)
        {
            if (parent == null) throw new ArgumentNullException("parent");

            lock (LockObject)
            {
                if (Parent != null)
                    throw new InvalidOperationException("This configuration element has been already added to the configuration tree!");

                Parent = parent;

                if (parent is ConfigElement parentCfg)
                    parentCfg.AddChild(this);

                OnAttached();
            }
        }

        protected virtual void OnAttached()
        {
        }

        internal void AddChild(ConfigElement child)
        {
            lock (LockObject)
            {
                ThrowIfImmutable();
                _children.Add(child);
            }
        }

        protected void ThrowIfImmutable()
        {
            
            Debug.Assert(Monitor.IsEntered(LockObject));

            if (IsLocked)
            {
                throw new InvalidOperationException("The configuration tree cannot be changed at this time! " +
                    "Please configure everything before starting!");
            }
        }

        internal void Lock()
        {
            lock (LockObject)
            {
                IsLocked = true;
                foreach (var _child in _children)
                    _child.Lock();
            }
        }

        internal void Init()
        {
            foreach (var _child in _children)
                _child.Init();

            ValidateAndInitialize();
        }

        protected virtual void ValidateAndInitialize()
        {
        }
    }
}
