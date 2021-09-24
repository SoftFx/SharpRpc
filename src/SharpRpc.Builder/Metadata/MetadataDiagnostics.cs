// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.Builder.Metadata
{
    public class MetadataDiagnostics
    {
        private readonly List<Diagnostic> _records = new List<Diagnostic>();

        public void AddOneWayReturnsDataWarning(Location location, string methodName)
        {
            var descriptor = CreateWarningDescriptor("RPC001", "One-way call returns data", "The remote procedure call '{0}' has a non-void return type while it is marked as a one-way message. One-way calls must have void as return type. The return type will be ignored.");
            AddRecord(descriptor, location, methodName);
        }

        public void DumpRecordsTo(GeneratorExecutionContext context)
        {
            foreach (var record in _records)
                context.ReportDiagnostic(record);
        }

        private void AddRecord(DiagnosticDescriptor descriptor, Location location, params object[] messageArgs)
        {
            _records.Add(Diagnostic.Create(descriptor, location, messageArgs));
        }

        private static DiagnosticDescriptor CreateWarningDescriptor(string id, string title, string messagePattern)
        {
            return new DiagnosticDescriptor(id, title, messagePattern, "RPC", DiagnosticSeverity.Warning, true);
        }

        private static DiagnosticDescriptor CreateErrorDescriptor(string id, string title, string messagePattern)
        {
            return new DiagnosticDescriptor(id, title, messagePattern, "RPC", DiagnosticSeverity.Error, true);
        }
    }
}
