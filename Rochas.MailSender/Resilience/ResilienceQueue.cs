using System;
using System.Collections.Concurrent;

namespace Rochas.Net.Connectivity
{
    internal static class ResilienceQueue<T>
    {
        private static readonly ConcurrentQueue<ResilienceSet> queue 
            = new ConcurrentQueue<ResilienceSet>();

        public static void Enqueue(ResilienceSet resilienceSet)
        {
            if (resilienceSet != null)
                queue?.Enqueue(resilienceSet);
        }

        public static ResilienceSet? Dequeue()
        {
            ResilienceSet? queueItem = null;

            if (!queue.IsEmpty)
            {
                queue?.TryPeek(out queueItem);
                if ((queueItem != null) && (queueItem.CallRetries == 0))
                    queue?.TryDequeue(out queueItem);
            }

            return queueItem;
        }
    }
}
