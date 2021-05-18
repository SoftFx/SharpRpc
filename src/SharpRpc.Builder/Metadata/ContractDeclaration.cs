// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpRpc.Builder
{
    public class ContractDeclaration
    {
        private List<SerializerDeclaration> _serializers = new List<SerializerDeclaration>();

        public ContractDeclaration(string typeFullName)
        {
            InterfaceName = new TypeString(typeFullName);
            FacadeClassName = new TypeString(InterfaceName.Namespace, InterfaceName.Short + "_Gen");
            MessageBundleClassName = new TypeString(FacadeClassName.Full, "Messages");
            SystemBundleClassName = new TypeString(FacadeClassName.Full, "SystemMessages");
            PrebuiltBundleClassName = new TypeString(FacadeClassName.Full, "PrebuiltMessages");
            MessageFactoryClassName = new TypeString(FacadeClassName.Full, "SystemMessagesFactory");
            BaseMessageClassName = new TypeString(MessageBundleClassName.Full, "MessageBase");
            LoginMessageClassName = new TypeString(SystemBundleClassName.Short, "Login");
            LogoutMessageClassName = new TypeString(SystemBundleClassName.Short, "Logout");
            HeartbeatMessageClassName = new TypeString(SystemBundleClassName.Short, "Heartbeat");
            //AuthDataClassName = new TypeString(SystemBundleClassName.Short, "AuthData");
            //BasicAuthDataClassName = new TypeString(SystemBundleClassName.Short, "BasicAuthData");
            ClientStubClassName = new TypeString(FacadeClassName.Full, "Client");
            ServiceStubClassName = new TypeString(FacadeClassName.Full, "Service");
        }

        public TypeString InterfaceName { get; }
        public TypeString FacadeClassName { get; }
        public TypeString MessageBundleClassName { get; }
        public TypeString SystemBundleClassName { get; }
        public TypeString PrebuiltBundleClassName { get; }
        public TypeString MessageFactoryClassName { get; }
        public TypeString ClientStubClassName { get; }
        public TypeString ServiceStubClassName { get; }
        public string Namespace => InterfaceName.Namespace;
        public TypeString BaseMessageClassName { get; }
        public TypeString LoginMessageClassName { get; }
        public TypeString LogoutMessageClassName { get; }
        public TypeString HeartbeatMessageClassName { get; }
        //public TypeString AuthDataClassName { get; }
        //public TypeString BasicAuthDataClassName { get; }
        public List<CallDeclaration> Calls { get; } = new List<CallDeclaration>();

        internal IReadOnlyList<SerializerDeclaration> Serializers => _serializers;

        internal string GetDefaultSerializerChoice()
        {
            if (Serializers.Count > 0)
                return Serializers[0].Builder.EnumVal;
            else
                return "DataContract";
        }

        internal void AddSerializer(SerializerBuilderBase serializerBuilder)
        {
            _serializers.Add(new SerializerDeclaration(serializerBuilder, InterfaceName));
        }

        public TypeString GetOnWayMessageClassName(string contractMethodName)
        {
            return GetMessageClassName(contractMethodName, Names.MessageClassPostfix);
        }

        public TypeString GetPrebuiltMessageClassName(string contracMethodName)
        {
            return new TypeString(PrebuiltBundleClassName.Short, contracMethodName);
        }

        public TypeString GetRequestClassName(string contractMethodName)
        {
            return GetMessageClassName(contractMethodName, Names.RequestClassPostfix);
        }

        public TypeString GetResponseClassName(string contractMethodName)
        {
            return GetMessageClassName(contractMethodName, Names.ResponseClassPostfix);
        }

        public TypeString GetMessageClassName(string contractMethodName, string postfix)
        {
            return new TypeString(MessageBundleClassName.Short, contractMethodName + postfix);
        }
    }
}
