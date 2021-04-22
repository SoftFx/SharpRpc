using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public abstract class ClientAuthenticator
    {
        public static ClientAuthenticator None { get; } = new Null();

        internal virtual ValueTask<string> OnLogin(ILoginMessage login)
        {
            return ValueTask.FromResult<string>(null);
        }

        private class Null : ClientAuthenticator
        {
        }
    }
}
