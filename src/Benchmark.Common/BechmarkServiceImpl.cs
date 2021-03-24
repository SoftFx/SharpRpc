using SharpRpc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Benchmark.Common
{
    public class BechmarkServiceImpl : BenchmarkContract_Gen.Service
    {
        public override ValueTask SendUpdate(FooEntity entity)
        {
            return new ValueTask();
        }

        public override ValueTask SendUpdate2(FooEntity entity)
        {
            return new ValueTask();
        }
    }
}
