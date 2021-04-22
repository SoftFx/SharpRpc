using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Benchmark.Server
{
    internal class AuthValidator : SharpRpc.PasswordValidator
    {
        public ValueTask<string> Validate(string userName, string password)
        {
            var valid = userName == "Admin" && password == "zzzz";

            if (!valid)
                return ValueTask.FromResult("Invalid credentials.");
            
            return ValueTask.FromResult<string>(null);
        }
    }
}
