// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Microsoft.CodeAnalysis;
using SharpRpc.Builder.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpRpc.Builder
{
    public class ContractDeclaration
    {
        private List<SerializerDeclaration> _serializers = new List<SerializerDeclaration>();

        public ContractDeclaration(string typeFullName, ContractCompatibility compatibility)
        {
            InterfaceName = new TypeString(typeFullName);
            Compatibility = compatibility;
            FacadeClassName = new TypeString(InterfaceName.Namespace, InterfaceName.Short + "_Gen");
            MessageBundleClassName = new TypeString(FacadeClassName.Full, "Messages");
            PrebuiltBundleClassName = new TypeString(FacadeClassName.Full, "PrebuiltMessages");

            BaseMessageClassName = new TypeString(MessageBundleClassName.Full, "MessageBase");
            LoginMessageClassName = new TypeString(MessageBundleClassName.Short, "Login");
            LogoutMessageClassName = new TypeString(MessageBundleClassName.Short, "Logout");
            FaultMessageClassName = new TypeString(MessageBundleClassName.Short, "RequestFault");
            StreamPageAckMessageClassName = new TypeString(MessageBundleClassName.Short, "PageAcknowledgement");
            StreamCloseMessageClassName = new TypeString(MessageBundleClassName.Short, "CloseStream");
            StreamCloseAckMessageClassName = new TypeString(MessageBundleClassName.Short, "CloseStreamAck");
            StreamCancelMessageClassName = new TypeString(MessageBundleClassName.Short, "CancelStream");
            HeartbeatMessageClassName = new TypeString(MessageBundleClassName.Short, "Heartbeat");
            CancelRequestMessageClassName = new TypeString(MessageBundleClassName.Short, "CancelRequest");

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
        public TypeString PrebuiltBundleClassName { get; }
        public TypeString MessageFactoryClassName => MessageBundleClassName;
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
        public TypeString StreamPageAckMessageClassName { get; }
        public TypeString StreamCloseMessageClassName { get; }
        public TypeString StreamCloseAckMessageClassName { get; }
        public TypeString StreamCancelMessageClassName { get; }
        public TypeString HeartbeatMessageClassName { get; }
        public TypeString CancelRequestMessageClassName { get; }

        public List<OperationDeclaration> Operations { get; } = new List<OperationDeclaration>();
        public ContractCompatibility Compatibility { get; }
        public bool EnablePrebuild { get; set; }
        public bool EnablePostResponseMethods { get; set; }

        public bool HasCallbacks => Operations.Any(c => c.IsCallback);

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

        public TypeString GetOnWayMessageClassName(OperationDeclaration callInfo)
        {
            return new TypeString(MessageBundleClassName.Short, callInfo.OneWayMessageName);
        }

        public TypeString GetPrebuiltMessageClassName(string contracMethodName)
        {
            return new TypeString(PrebuiltBundleClassName.Short, contracMethodName);
        }

        public TypeString GetRequestClassName(OperationDeclaration callInfo)
        {
            return new TypeString(MessageBundleClassName.Short, callInfo.RequestMessageName);
        }

        public TypeString GetResponseClassName(OperationDeclaration callInfo)
        {
            return new TypeString(MessageBundleClassName.Short, callInfo.ResponseMessageName);
        }

        public TypeString GetFaultMessageClassName(OperationDeclaration opInfo)
        {
            return new TypeString(MessageBundleClassName.Short, opInfo.FaultMessageName);
        }

        public TypeString GetFaultAdapterClassName(ushort faultKey, OperationDeclaration opInfo)
        {
            var faultMsgType = GetFaultMessageClassName(opInfo);

            return new TypeString(faultMsgType.Full, "F" + faultKey + "_Adapter");
        }

        public TypeString GetInputStreamMessageClassName(OperationDeclaration operation)
        {
            return new TypeString(MessageBundleClassName.Short, "C" + operation.Key + "_InputPage");
        }

        public TypeString GetOutputStreamMessageClassName(OperationDeclaration operation)
        {
            return new TypeString(MessageBundleClassName.Short, "C" + operation.Key +  "_OutputPage");
        }

        public TypeString GetInputStreamFactoryClassName(OperationDeclaration operation)
        {
            return new TypeString(MessageBundleClassName.Short, "C" + operation.Key + "_InputStreamFactory");
        }

        public TypeString GetOutputStreamFactoryClassName(OperationDeclaration operation)
        {
            return new TypeString(MessageBundleClassName.Short, "C" + operation.Key + "_OutputStreamFactory");
        }

        public void Validate(MetadataDiagnostics diagnostics)
        {
            var keySet = new HashSet<ushort>();

            foreach (var op in Operations.ToList())
            {
                if (keySet.Contains(op.Key))
                {
                    diagnostics.AddDupKeyError(op.CodeLocation, op.MethodName, op.Key);
                    Operations.Remove(op);
                }
                else
                    keySet.Add(op.Key);
            }
        }
    }
}
