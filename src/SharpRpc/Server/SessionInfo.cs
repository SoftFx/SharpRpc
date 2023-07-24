// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public class SessionInfo
    {
        private Channel _ch;

        internal void Init(Channel channel, SessionContext sharedContext, TransportInfo transportInfo)
        {
            _ch = channel;
            Id = _ch.Id;
            Properties = sharedContext.Properties;
            TransportInfo = transportInfo;
        }

        public string Id { get; private set; }

        public CustomProperties Properties { get; private set; }
        public TransportInfo TransportInfo { get; private set; }

        /// <summary>
        /// Triggers session close. It's safe to call this function from message/call handlers.
        /// </summary>
        public void BeginClose()
        {
            _ch.TriggerClose();
        }

        public T GetTransportInfo<T>()
            where T : TransportInfo
        {
            if (TransportInfo is T typedInfo)
                return typedInfo;

            return default(T);
        }

#if PF_COUNTERS
        public double AverageRxBufferSize => _ch.GetAverageRxBufferSize();
        public double AverageRxMessagesPerBuffer => _ch.GetAverageRxMessageBatchSize();
        public double AverageRxMessageSize => _ch.GetAverageRxMessageSize();
        public int MessageCount => _ch.GetRxMessageCount();
#endif
    }
}
