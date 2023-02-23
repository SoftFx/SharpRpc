// Copyright © 2021 Soft-Fx. All rights reserved.
// Author: Andrei Hilevich
//
// This Source Code Form is subject to the terms of the Mozilla
// Public License, v. 2.0. If a copy of the MPL was not distributed
// with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using TestCommon;

namespace TestClient
{
    public abstract class TestBase
    {
        public TestBase()
        {
            Name = GetType().Name;
        }

        public string Name { get; }

        public abstract void RunTest(TestCase tCase, FunctionTestContract_Gen.Client client);

        public virtual TestCase GetRandomCase(Random rnd)
        {
            return new TestCase(this);
        }

        public virtual IEnumerable<TestCase> GetPredefinedCases()
        {
            yield return new TestCase(this);
        }

        public static bool NullCertValidator(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }

    public class TestCase
    {
        private readonly Dictionary<string, object> _params = new Dictionary<string, object>();

        public TestCase(TestBase test)
        {
            Test = test;
        }

        public TestBase Test { get; }

        public void PrintCaseParams(StringBuilder builder)
        {
            bool first = true;

            foreach (var entry in _params)
            {
                if (first)
                    first = false;
                else
                    builder.Append(", ");

                builder.Append(entry.Key);
                builder.Append("=");
                builder.Append(entry.Value);
            }
        }

        public void RunTest(FunctionTestContract_Gen.Client client)
        {
            Test.RunTest(this, client);
        }

        public T GetParam<T>(string paramName)
        {
            return (T)_params[paramName];
        }

        public TestCase SetParam(string name, object value)
        {
            _params[name] = value;
            return this;
        }

        public object this[string paramName]
        {
            get => _params[paramName];
            set => _params[paramName] = value;
        }
    }
}
