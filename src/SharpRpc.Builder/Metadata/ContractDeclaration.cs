// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpRpc.Builder
{
    public class ContractDeclaration
    {
        private List<SerializerDeclaration> _serializers = new List<SerializerDeclaration>();
        private List<string> _faultTypesById = new List<string>();
        private List<string> _streamTypesById = new List<string>();

        public ContractDeclaration(string typeFullName, ContractCompatibility compatibility)
        {
            InterfaceName = new TypeString(typeFullName);
            Compatibility = compatibility;
            FacadeClassName = new TypeString(InterfaceName.Namespace, InterfaceName.Short + "_Gen");
            MessageBundleClassName = new TypeString(FacadeClassName.Full, "Messages");
            SystemBundleClassName = new TypeString(FacadeClassName.Full, "SystemMessages");
            PrebuiltBundleClassName = new TypeString(FacadeClassName.Full, "PrebuiltMessages");
            StreamBundleClassName = new TypeString(FacadeClassName.Full, "StreamMessages");
            MessageFactoryClassName = new TypeString(FacadeClassName.Full, "SystemMessagesFactory");
            BaseMessageClassName = new TypeString(MessageBundleClassName.Full, "MessageBase");
            LoginMessageClassName = new TypeString(SystemBundleClassName.Short, "Login");
            LogoutMessageClassName = new TypeString(SystemBundleClassName.Short, "Logout");
            FaultMessageClassName = new TypeString(SystemBundleClassName.Short, "RequestFault");
            HeartbeatMessageClassName = new TypeString(SystemBundleClassName.Short, "Heartbeat");
            //AuthDataClassName = new TypeString(SystemBundleClassName.Short, "AuthData");
            //BasicAuthDataClassName = new TypeString(SystemBundleClassName.Short, "BasicAuthData");
            ClientStubClassName = new TypeString(FacadeClassName.Full, "Client");
            ServiceStubClassName = new TypeString(FacadeClassName.Full, "ServiceBase");
            ServiceHandlerClassName = new TypeString(FacadeClassName.Full, "ServiceHandler");
            CallbackClientStubClassName = new TypeString(FacadeClassName.Full, "CallbackClient");
            CallbackServiceStubClassName = new TypeString(FacadeClassName.Full, "CallbackServiceBase");
            CallbackHandlerClassName = new TypeString(FacadeClassName.Full, "CallbackServiceHandler");
        }

        public TypeString InterfaceName { get; }
        public TypeString FacadeClassName { get; }
        public TypeString MessageBundleClassName { get; }
        public TypeString SystemBundleClassName { get; }
        public TypeString PrebuiltBundleClassName { get; }
        public TypeString StreamBundleClassName { get; }
        public TypeString MessageFactoryClassName { get; }
        public TypeString ClientStubClassName { get; }
        public TypeString CallbackClientStubClassName { get; }
        public TypeString ServiceStubClassName { get; }
        public TypeString ServiceHandlerClassName { get; }
        public TypeString CallbackServiceStubClassName { get; }
        public TypeString CallbackHandlerClassName { get; }
        public string Namespace => InterfaceName.Namespace;
        public TypeString BaseMessageClassName { get; }
        public TypeString LoginMessageClassName { get; }
        public TypeString LogoutMessageClassName { get; }
        public TypeString FaultMessageClassName { get; }
        public TypeString HeartbeatMessageClassName { get; }
        public List<CallDeclaration> Calls { get; } = new List<CallDeclaration>();
        public List<string> FaultTypes => _faultTypesById;
        public List<string> StreamTypes => _streamTypesById;
        public ContractCompatibility Compatibility { get; }

        public bool HasCallbacks => Calls.Any(c => c.IsCallback);
        public bool HasStreams => _streamTypesById.Count > 0;

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

        public TypeString GetCustomFaultMessageClassName(string faultDataType)
        {
            var id = GetFaultTypeId(faultDataType);

            return new TypeString(SystemBundleClassName.Short, "CustomRequestFault" + id);
        }

        public TypeString GetMessageClassName(string contractMethodName, string postfix)
        {
            return new TypeString(MessageBundleClassName.Short, contractMethodName + postfix);
        }

        public void RegisterFault(string faultType)
        {
            if (!_faultTypesById.Contains(faultType))
                _faultTypesById.Add(faultType);
        }

        private int GetFaultTypeId(string faultType)
        {
            var index = _faultTypesById.IndexOf(faultType);

            if (index < 0)
                throw new Exception("Fault type is not registered: " + faultType);

            return index;
        }

        public TypeString GetStreamPageClassName(string type)
        {
            var id = GetStreamTypeId(type);

            return new TypeString(StreamBundleClassName.Short, "Page" + id);
        }

        public TypeString GetStreamFactoryClassName(string type)
        {
            var id = GetStreamTypeId(type);

            return new TypeString(StreamBundleClassName.Short, "Factory" + id);
        }

        public void RegisterStreamType(string type)
        {
            if (!_streamTypesById.Contains(type))
                _streamTypesById.Add(type);
        }

        private int GetStreamTypeId(string type)
        {
            var index = _streamTypesById.IndexOf(type);

            if (index < 0)
                throw new Exception("Stream type is not registered: " +  type);

            return index;
        }
    }
}
