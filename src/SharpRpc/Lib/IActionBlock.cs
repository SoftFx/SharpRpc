using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharpRpc.Lib
{
    public interface IActionBlock<T>
    {
        bool TryEnqueue(T item);
        ValueTask<bool> TryEnqueueAsync(T item);
    }
}
