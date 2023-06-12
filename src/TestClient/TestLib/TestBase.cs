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

namespace TestClient.TestLib
{
    public abstract class TestBase
    {
        public TestBase()
        {
            Name = GetType().Name;
        }

        public string Name { get; }

        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is TestBase tb)
                return tb.Name == Name;

            return false;
        }

        public abstract void RunTest(TestCase tCase);
    }

    public class TestCase
    {
        private readonly Dictionary<string, ParamValue> _params = new Dictionary<string, ParamValue>();

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
                if (entry.Value.DisplayValue != null) // skip hidden
                {
                    if (first)
                        first = false;
                    else
                        builder.Append(", ");

                    //builder.Append(entry.Key);
                    //builder.Append("=");
                    builder.Append(entry.Value.DisplayValue);
                }
            }
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            PrintCaseParams(builder);
            return builder.ToString();
        }

        public void RunTest()
        {
            Test.RunTest(this);
        }

        public T GetParam<T>(string paramName)
        {
            return (T)_params[paramName].Value;
        }

        public TestCase SetHiddenParam(string name, object value)
        {
            _params[name] = new ParamValue(value, null);
            return this;
        }

        public TestCase SetParam(string name, object value)
        {
            _params[name] = new ParamValue(value, value?.ToString());
            return this;
        }

        public TestCase SetParam(string name, string displayValue, object value)
        {
            _params[name] = new ParamValue(value, displayValue);
            return this;
        }

        public object this[string paramName]
        {
            get => _params[paramName].Value;
        }

        protected struct ParamValue
        {
            public ParamValue(object value, string dValue)
            {
                Value = value;
                DisplayValue = dValue;
            }

            public object Value { get; }
            public string DisplayValue { get; }
        }
    }
}
