// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SharpRpc
{
    internal abstract class StreamWriteCoordinator
    {
        public IStreamCoordinatorContext Context { get; private set; }

        public virtual StreamWriteCoordinator Init(IStreamCoordinatorContext context)
        {
            Context = context;
            return this;
        }

        public bool IsBlocked { get; private set; }
        public int WindowFill { get; private set; }

        public abstract bool CanSend();

        public virtual void OnPageSent(int pageSize)
        {
            Debug.Assert(Monitor.IsEntered(Context.SyncObj));

            WindowFill++;

            if (WindowFill >= Context.MaxPageCount)
                IsBlocked = true;
        }

        public virtual void OnAcknowledgementRx(IStreamPageAck ack)
        {
            Debug.Assert(Monitor.IsEntered(Context.SyncObj));

            WindowFill--;

            if (WindowFill < Context.MaxPageCount)
                IsBlocked = false;
        }

        public class Realtime : StreamWriteCoordinator
        {
            public override bool CanSend() => !IsBlocked;
        }

        public class Greedy : StreamWriteCoordinator
        {
            public override bool CanSend()
            {
                if (IsBlocked)
                    return false;

                return Context.QueueSize >= Context.MaxPageSize
                    || Context.IsCompleted;
            }   
        }

        public class Balanced : StreamWriteCoordinator
        {
            private int _firstThreshold;
            private int _secondThreshold;

            public override StreamWriteCoordinator Init(IStreamCoordinatorContext context)
            {
                base.Init(context);

                _firstThreshold = context.MaxPageSize / 2;
                _secondThreshold = context.MaxPageSize;

                return this;
            }

            public override bool CanSend()
            {
                if (IsBlocked)
                    return false;

                if (WindowFill < 2)
                    return true;
                else if (WindowFill < 5)
                    return Context.QueueSize >= _firstThreshold || Context.IsCompleted;
                else
                    return Context.QueueSize >= _secondThreshold || Context.IsCompleted;
            }
        }
    }

    internal interface IStreamCoordinatorContext
    {
        object SyncObj { get; }
        int QueueSize { get; }
        int MaxPageSize { get; }
        int MaxPageCount { get; }
        bool IsCompleted { get; }
    }
}
    