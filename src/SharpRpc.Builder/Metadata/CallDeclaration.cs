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
    public class CallDeclaration
    {
        public CallDeclaration(string methodName, Location codeLocation, ContractCallType type)
        {
            MethodName = methodName;
            CallType = type;
            CodeLocation = codeLocation;
        }

        public string MethodName { get; }
        public Location CodeLocation { get; }
        public ContractCallType CallType { get; }
        public List<string> Faults { get; } = new List<string>();
        public List<ParamDeclaration> Params { get; } = new List<ParamDeclaration>();
        public ParamDeclaration ReturnParam { get; set; }
        public bool EnablePrebuild { get; set; }
        public string InStreamItemType { get; set; }
        public string OutStreamItemType { get; set; }

        public bool IsOneWay => CallType == ContractCallType.MessageToClient || CallType == ContractCallType.MessageToServer;
        public bool IsCallback => CallType == ContractCallType.CallToClient || CallType == ContractCallType.MessageToClient;
        public bool IsRequestResponceCall => CallType == ContractCallType.CallToClient || CallType == ContractCallType.CallToServer;
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
    }
}
