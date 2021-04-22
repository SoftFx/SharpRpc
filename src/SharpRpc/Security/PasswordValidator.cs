using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc
{
    public interface PasswordValidator
    {
        /// <summary>
        /// Implement this method to provide basic login/password authentication.
        /// </summary>
        /// <param name="userName">User name to validate.</param>
        /// <param name="password">Password to validate.</param>
        /// <returns>Returns null if password is valid, otherwise error message.</returns>
        ValueTask<string> Validate(string userName, string password);
    }
}
