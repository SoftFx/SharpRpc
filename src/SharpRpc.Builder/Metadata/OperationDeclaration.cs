// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpRpc.Builder
{
    public class OperationDeclaration
    {
        public const int KeyReserve = 30;

        public OperationDeclaration(ushort key, string methodName, Location codeLocation, ContractCallType type)
        {
            Key = key;
            MethodName = methodName;
            CallType = type;
            CodeLocation = codeLocation;

            RequestKey = KeyReserve + key * 5;
            ResponseKey = RequestKey + 1;
            FaultKey = RequestKey + 2;
            InStreamPageKey = RequestKey + 3;
            OutStreamPageKey = RequestKey + 4;

            OneWayMessageName = "C" + key + "_Message";
            RequestMessageName = "C" + key + "_Request";
            ResponseMessageName = "C" + key + "_Response";
            FaultMessageName = "C" + key + "_Fault";
            InputPageMessageName = "C" + key + "_InputStreamPage";
            OutputPageMessageName = "C" + key + "_OutputStreamPage";
            FaultAdapterInterfaceName = "IFaultAdapter";
        }

        public ushort Key { get; }
        public string MethodName { get; }
        public Location CodeLocation { get; }
        public ContractCallType CallType { get; }
        public List<Tuple<ushort, string>> CustomFaults { get; } = new List<Tuple<ushort, string>>();
        public List<ParamDeclaration> Params { get; } = new List<ParamDeclaration>();
        public ParamDeclaration ReturnParam { get; set; }
        public string InStreamItemType { get; set; }
        public string OutStreamItemType { get; set; }

        public int RequestKey { get; }
        public int ResponseKey { get; }
        public int FaultKey { get; }
        public int InStreamPageKey { get; }
        public int OutStreamPageKey { get; }

        public string OneWayMessageName { get; }
        public string RequestMessageName { get; }
        public string ResponseMessageName { get; }
        public string FaultMessageName { get; }
        public string InputPageMessageName { get; }
        public string OutputPageMessageName { get; }
        public string FaultAdapterInterfaceName { get; }

        public bool IsOneWay => CallType == ContractCallType.MessageToClient || CallType == ContractCallType.MessageToServer;
        public bool IsCallback => CallType == ContractCallType.CallToClient || CallType == ContractCallType.MessageToClient;
        public bool IsRequestResponceCall => CallType == ContractCallType.CallToClient || CallType == ContractCallType.CallToServer || HasStreams;
        public bool ReturnsData => ReturnParam != null && !string.IsNullOrEmpty(ReturnParam.ParamType);
        public bool HasInStream => !string.IsNullOrEmpty(InStreamItemType);
        public bool HasOutStream => !string.IsNullOrEmpty(OutStreamItemType);
        public bool HasStreams => HasInStream || HasOutStream;

        public override string ToString()
        {
            var builder = new StringBuilder();

            if (ReturnParam == null)
                builder.Append("void");
            else
                builder.Append(ReturnParam.ParamType);

            builder.Append(" ").Append(MethodName);
            builder.Append("(");
            builder.Append(string.Join(",", Params.Select(p => p.ParamType)));
            builder.Append(")");

            return builder.ToString();
        }

        public bool HasParameterWithName(string name)
        {
            return Params.Any(p => p.ParamName == name);
        }

        public void AddFault(ushort key, string type)
        {
            CustomFaults.Add(Tuple.Create(key, type));
        }
    }
}
