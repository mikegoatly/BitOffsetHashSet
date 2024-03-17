using System.Collections.Concurrent;

namespace Goatly.BitOffsetHashSets
{
    internal static class BitBufferCache
    {
        private static readonly int maxChunkSize = BitOffsetHashSetConfiguration.DefaultCacheChunkSize;
        private static readonly object unallocatedLock = new();

        // Buckets for different chunk sizes for quick access, using a concurrent dictionary
        private static readonly ConcurrentDictionary<int, ConcurrentStack<Memory<ulong>>> buckets = new();

        private static Memory<ulong> unallocated = new ulong[maxChunkSize];

        internal static Memory<ulong> Lease(int length)
        {
            if (length > maxChunkSize)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Requested length is greater than the default cache chunk size.");
            }

            if (!buckets.TryGetValue(length, out var stack) || stack.IsEmpty)
            {
                // Synchronize the allocation of new chunks to prevent over-allocation
                lock (unallocatedLock)
                {
                    if (unallocated.Length < length)
                    {
                        ExpandUnallocated();
                    }

                    var result = unallocated[..length];
                    unallocated = unallocated[length..];
                    return result;
                }
            }

            if (stack.TryPop(out var chunk))
            {
                return chunk;
            }

            // Fallback in case of race condition where the stack is empty after check but before pop
            // Here we recurse to try again, but this time the stack will be empty and we'll allocate a new chunk
            return Lease(length);
        }

        private static void ExpandUnallocated()
        {
            // Since this method is called inside a lock, it's already thread-safe.
            Return(unallocated);
            unallocated = new ulong[maxChunkSize];
        }

        internal static void Return(Memory<ulong> chunk)
        {
            // Clear the chunk for reuse
            chunk.Span.Clear();

            var bucket = buckets.GetOrAdd(chunk.Length, _ => new ConcurrentStack<Memory<ulong>>());
            bucket.Push(chunk);
        }
    }
}
