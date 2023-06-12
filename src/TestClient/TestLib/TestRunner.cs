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

namespace TestClient.TestLib
{
    internal class TestRunner
    {
        private readonly Dictionary<TestBase, List<TestCase>> _casesByTest = new Dictionary<TestBase, List<TestCase>>();

        public void AddCase(TestCase testCase)
        {
            if (!_casesByTest.TryGetValue(testCase.Test, out var caseList))
            {
                caseList = new List<TestCase>();
                _casesByTest.Add(testCase.Test, caseList);
            }

            caseList.Add(testCase);
        }

        public void AddCases(IEnumerable<TestCase> cases)
        {
            foreach (var testCase in cases)
                AddCase(testCase);
        }

        public void RunAll()
        {
            int total = 0;
            int failed = 0;

            foreach (var entry in _casesByTest)
            {
                var test = entry.Key;

                Console.WriteLine("TEST " + test.Name);

                foreach (var tCase in entry.Value)
                {
                    Console.Write("\tCASE " + tCase);

                    total++;

                    try
                    {
                        tCase.RunTest();
                        Console.WriteLine(" - OK");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(" - FAILED");
                        Console.WriteLine(ex);
                        failed++;
                    }
                }   
            }

            Console.WriteLine();
            Console.WriteLine($"Tests: {total}, failed: {failed}.");

            if (failed > 0)
            {
                Console.WriteLine();
                Console.WriteLine("WARNING! Some tests are failed! WARNING!");
            }
        }
    }
}
