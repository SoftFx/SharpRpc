// Copyright © 2022 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc.Server
{
    internal class ServiceRegistry
    {
        private readonly ServiceGroup _namelessGroup = new ServiceGroup(string.Empty);
        private readonly Dictionary<string, ServiceGroup> _groupsByName = new Dictionary<string, ServiceGroup>();

        public void Add(ServiceBinding service)
        {
            _namelessGroup.Add(service);
        }

        public void Add(string serviceName, ServiceBinding service)
        {
            var normalizedServiceName = serviceName.Trim().ToLowerInvariant();

            if (!_groupsByName.TryGetValue(normalizedServiceName, out ServiceGroup serviceGroup))
            {
                serviceGroup = new ServiceGroup(normalizedServiceName);
                _groupsByName.Add(normalizedServiceName, serviceGroup);
            }

            serviceGroup.Add(service);
        }

        public enum ResolveRetCode { Ok, ServiceNotFound, HostNameNotFound }

        public ResolveRetCode TryResolve(string hostName, string serviceName, out ServiceBinding binding)
        {
            var normalizedHostName = hostName.Trim().ToLowerInvariant();
            var normalizedServiceName = serviceName.Trim().ToLowerInvariant();

            if (normalizedServiceName.Length == 0)
            {
                binding = _namelessGroup.ResolveService(normalizedHostName);
                return binding == null ? ResolveRetCode.HostNameNotFound : ResolveRetCode.Ok;
            }

            if (!_groupsByName.TryGetValue(normalizedServiceName, out var group))
            {
                binding = null;
                return ResolveRetCode.ServiceNotFound;
            }

            binding = group.ResolveService(normalizedHostName);
            return binding == null ? ResolveRetCode.HostNameNotFound : ResolveRetCode.Ok;
        }

        public void BuildCache()
        {
            _namelessGroup.BuildCache();

            foreach (var group in _groupsByName.Values)
                group.BuildCache();
        }

        private class ServiceGroup
        {
            private readonly List<ServiceBinding> _bindings = new List<ServiceBinding>();
            private ServiceBinding _defaultBinding;
            private readonly Dictionary<string, ServiceBinding> _byHost = new Dictionary<string, ServiceBinding>();

            public ServiceGroup(string serviceName)
            {
                Name = serviceName;
            }

            public string Name { get; }

            public void Add(ServiceBinding binding)
            {
                _bindings.Add(binding);
            }

            public ServiceBinding ResolveService(string hostName)
            {
                if (_byHost.TryGetValue(hostName, out var serviceBinding))
                    return serviceBinding;

                return _defaultBinding;
            }

            public void BuildCache()
            {
                foreach (var serviceBinding in _bindings)
                {
                    if (serviceBinding.AcceptsAnyHostName)
                    {
                        if (_defaultBinding != null)
                            throw new RpcConfigurationException($"There are multiple services bindings for the name '{Name}' with default host resolve (no hostnames). " +
                                $"Only one of those bindings can be default host resolve, others should have distinct hostnames.");
                        _defaultBinding = serviceBinding;
                    }
                    else
                    {
                        foreach (var hostName in serviceBinding.AcceptedHostNames)
                        {
                            if (_byHost.ContainsKey(hostName))
                                throw new RpcConfigurationException($"Service bindings contain a duplicate hostname/service pair: '{hostName}'/'{Name}'!");
                            _byHost[hostName] = serviceBinding;
                        }
                    }
                }
            }
        }
    }
}
