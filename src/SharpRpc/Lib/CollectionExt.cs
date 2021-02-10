using System;
using System.Collections.Generic;
using System.Text;

namespace SharpRpc.Lib
{
    public static class CollectionExt
    {
        public static void DequeueRange<T>(this Queue<T> queue, List<T> toContainer, int maxItems)
        {
            while (toContainer.Count < maxItems)
            {
                if (queue.Count > 0)
                    toContainer.Add(queue.Dequeue());
                else
                    break;
            }
        }
    }
}
