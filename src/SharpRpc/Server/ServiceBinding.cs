// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using SharpRpc.Config;
using SharpRpc.Server;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc
{
    public class ServiceBinding : ConfigElement
    {
        private readonly HashSet<string> _hostNames = new HashSet<string>();
        private Authenticator _authenticator = Authenticator.None;

        public ServiceBinding(string serviceName, ServiceDescriptor descriptor)
        {
            //if (string.IsNullOrWhiteSpace(serviceName))
            //    throw new ArgumentException("Service name is invalid!");

            ServiceName = serviceName;
            NormalizedServiceName = serviceName?.Trim().ToLowerInvariant();
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        }

        public string ServiceName { get; }
        public ServiceDescriptor Descriptor { get; }
        internal string NormalizedServiceName { get; }

        public bool AcceptsAnyHostName => _hostNames.Count == 0;
        public IEnumerable<string> AcceptedHostNames => _hostNames;

        public Authenticator Authenticator
        {
            get => _authenticator;
            set
            {
                lock (LockObject)
                {
                    ThrowIfImmutable();
                    _authenticator = value ?? throw new ArgumentNullException(nameof(value));
                }
            }
        }

        internal RpcCallHandler CreateServiceImpl()
        {
            return Descriptor.ServiceImplFactory();
        }

        protected void AddAcceptedHostName(string hostName)
        {
            if (string.IsNullOrWhiteSpace(hostName))
                throw new ArgumentException("Domain name is invalid!");

            if (Uri.CheckHostName(hostName) == UriHostNameType.Unknown)
                throw new ArgumentException("Domain name is invalid!");

            var uri = new Uri(hostName);

            lock (LockObject)
            {
                ThrowIfImmutable();
                _hostNames.Add(hostName);
            }
        }
    }
}
