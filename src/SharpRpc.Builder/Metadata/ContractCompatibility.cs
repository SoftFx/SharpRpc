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
    public class ContractCompatibility
    {
        public ContractCompatibility(GeneratorExecutionContext context)
        {
            IsNet5 = context.ParseOptions.PreprocessorSymbolNames.Contains("NET5_0_OR_GREATER");
            SupportsPatternMatching = IsNet5;
        }

        public bool IsNet5 { get; }
        public bool SupportsPatternMatching { get; }

        public string GetAsyncWrapper()
        {
            if (IsNet5)
                return Names.SystemValueTask;
            else
                return Names.SystemTask;
        }
    }
}
