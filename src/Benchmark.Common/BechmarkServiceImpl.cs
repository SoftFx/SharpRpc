using SharpRpc;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Benchmark.Common
{
    public class BechmarkServiceImpl : Contract_Service
    {
        public override Task SendUpdate2Async(FooEntity entity)
        {
            throw new NotImplementedException();
        }

        public override Task SendUpdateAsync(FooEntity entity, int index)
        {
            throw new NotImplementedException();
        }
    }
}
