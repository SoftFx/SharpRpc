// Copyright © 2022 Soft-Fx. All rights reserved.
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

namespace SharpRpc.Tcp
{
    public class SocketServiceBinding : ServiceBinding
    {
        private TcpServerSecurity _security = TcpServerSecurity.None;

        internal SocketServiceBinding(string serviceName, ServiceDescriptor descriptor)
            : base(serviceName, descriptor)
        {
        }

        public TcpServerSecurity Security
        {
            get => _security;
            set
            {
                lock (LockObject)
                {
                    ThrowIfImmutable();
                    _security = value ?? throw new ArgumentNullException(nameof(Security));
                }
            }
        }

        protected override void ValidateAndInitialize()
        {
            base.ValidateAndInitialize();

            _security.Init();
        }
    }

    public class TcpServiceBinding : SocketServiceBinding
    {
        internal TcpServiceBinding(string serviceName, ServiceDescriptor descriptor)
            : base(serviceName, descriptor)
        {
        }

        public TcpServiceBinding SetSecurity(TcpServerSecurity security)
        {
            Security = security;
            return this;
        }

        public TcpServiceBinding AcceptHostNames(params string[] names)
        {
            foreach (var name in names)
                AddAcceptedHostName(name);

            return this;
        }

        public TcpServiceBinding SetAuthenticator(Authenticator authenticator)
        {
            Authenticator = authenticator;
            return this;
        }
    }

    public class UdsServiceBinding : SocketServiceBinding
    {
        internal UdsServiceBinding(string serviceName, ServiceDescriptor descriptor)
            : base(serviceName, descriptor)
        {
        }

        public UdsServiceBinding SetSecurity(TcpServerSecurity security)
        {
            Security = security;
            return this;
        }

        public UdsServiceBinding SetAuthenticator(Authenticator authenticator)
        {
            Authenticator = authenticator;
            return this;
        }
    }
}
