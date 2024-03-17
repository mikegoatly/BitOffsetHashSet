using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Goatly.BitOffsetHashSets
{
    /// <summary>
    /// Configurations for the <see cref="BitOffsetHashSet"/> behaviors.
    /// </summary>
    public static class BitOffsetHashSetConfiguration
    {
        /// <summary>
        /// Controls how much memory is allocated up-front for the internal cache of bit buffers.
        /// Defaults to 64.
        /// </summary>
        public static int DefaultCacheChunkSize { get; set; } = 64;
    }

    public sealed class BitOffsetHashSet : IDisposable, IEnumerable<int>
    {
        private int count;
        private int baseOffset;
        private Memory<ulong> bitData;

        public BitOffsetHashSet(int initialCapacity = 0)
        {
            bitData = initialCapacity == 0 ? Memory<ulong>.Empty : BitBufferCache.Lease(initialCapacity);
        }

        internal int BitDataBufferLength => bitData.Length;

        public int Count => count;

        public bool Add(int value)
        {
            int index;
            if (count == 0)
            {
                index = 0;
                baseOffset = value;
            }
            else 
            {
                if ((value < baseOffset))
                {
                    ShiftBaseTo(value);
                }

                index = (value - baseOffset) / 64;
            }

            EnsureCapacity(index + 1);

            var bit = CalculateBit(value, index);
            var alreadySet = (this.bitData.Span[index] & bit) != 0;
            if (!alreadySet)
            {
                this.bitData.Span[index] |= bit;
                this.count++;
                return true;
            }

            return false;
        }

        public bool Contains(int value)
        {
            if (value < baseOffset)
            {
                return false;
            }

            int index = (value - baseOffset) / 64;
            if (index >= bitData.Length)
            {
                return false;
            }

            return (this.bitData.Span[index] & CalculateBit(value, index)) != 0;
        }

        public bool Remove(int value)
        {
            if (value < baseOffset)
            {
                return false;
            }

            int index = (value - baseOffset) / 64;
            if (index >= bitData.Length)
            {
                return false;
            }

            var bit = CalculateBit(value, index);
            var current = this.bitData.Span[index];
            var alreadySet = (current & bit) != 0;
            if (alreadySet)
            {
                var newValue = current & ~bit;
                this.bitData.Span[index] = newValue;
                this.count--;

                return true;
            }

            return false;
        }

        public bool Compact()
        {
            int lastNonZeroBlock = bitData.Length - 1;
            while (lastNonZeroBlock >= 0 && bitData.Span[lastNonZeroBlock] == 0)
            {
                lastNonZeroBlock--;
            }

            if (lastNonZeroBlock == -1)
            {
                // The list is empty
                BitBufferCache.Return(bitData);
                bitData = Memory<ulong>.Empty;
                baseOffset = 0;
                count = 0;
                return true;
            }

            int firstNonZeroBlock = 0;
            while (firstNonZeroBlock < bitData.Length && bitData.Span[firstNonZeroBlock] == 0)
            {
                firstNonZeroBlock++;
            }

            if (firstNonZeroBlock == 0 && lastNonZeroBlock == bitData.Length - 1)
            {
                // No need to compact
                return false;
            }

            // Request a new chunk of the right size
            var newBitData = BitBufferCache.Lease(lastNonZeroBlock - firstNonZeroBlock + 1);
            bitData.Span[firstNonZeroBlock..(lastNonZeroBlock + 1)].CopyTo(newBitData.Span);

            if (firstNonZeroBlock > 0)
            {
                // Adjust the base offset
                baseOffset += firstNonZeroBlock * 64;
            }

            var leadingZeroCount = BitOperations.LeadingZeroCount(newBitData.Span[0]);
            if (leadingZeroCount > 0)
            {
                // Shift all the data to the left
                for (int i = 0; i < newBitData.Length - 1; i++)
                {
                    newBitData.Span[i] = (newBitData.Span[i] << leadingZeroCount) | (newBitData.Span[i + 1] >> (64 - leadingZeroCount));
                }

                newBitData.Span[^1] = newBitData.Span[^1] << leadingZeroCount;

                // Adjust the base offset
                baseOffset -= leadingZeroCount;
            }

            BitBufferCache.Return(bitData);
            bitData = newBitData;

            return true;
        }

        public void Dispose()
        {
            if (bitData.Length > 0)
            {
                BitBufferCache.Return(bitData);
                bitData = null;
            }
        }

        public IEnumerator<int> GetEnumerator()
        {
            int enumeratedCount = 0;
            int offset = baseOffset;
            int index = 0;
            while (enumeratedCount < count)
            {
                ulong current = bitData.Span[index];
                while (current != 0)
                {
                    int bit = BitOperations.TrailingZeroCount(current);
                    yield return offset + bit;
                    current &= current - 1;
                    enumeratedCount++;
                }

                offset += 64;
                index++;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public void Clear()
        {
            if (bitData.Length > 0)
            {
                // Return the chunk to the cache
                BitBufferCache.Return(bitData);
            }

            bitData = Memory<ulong>.Empty;
            count = 0;
        }

        private void ShiftBaseTo(int newBaseOffset)
        {
            if (newBaseOffset >= baseOffset)
            {
                throw new ArgumentOutOfRangeException(nameof(newBaseOffset), "The new base offset must be less than the current base offset");
            }

            // We need to bit shift existing data - possibly allocating a new chunk if we drop off to the right
            int totalBitShift = baseOffset - newBaseOffset;
            baseOffset = newBaseOffset;

            var extraLeadingBlocks = totalBitShift / 64;
            var blockBitShift = totalBitShift % 64;
            var requiresOverflowToNewBlock = BitOperations.LeadingZeroCount(bitData.Span[^1]) < blockBitShift;

            // Determine if we need to allocate a new chunk
            if (requiresOverflowToNewBlock || extraLeadingBlocks > 0)
            {
                var newBitData = BitBufferCache.Lease(bitData.Length + extraLeadingBlocks + (requiresOverflowToNewBlock ? 1 : 0));
                bitData.Span.CopyTo(newBitData.Span[extraLeadingBlocks..]);
                BitBufferCache.Return(bitData);
                bitData = newBitData;
            }

            if (blockBitShift == 0)
            {
                // The new base offset is a multiple of 64, so we don't need to shift the data
                return;
            }

            // Work through the existing data and shift it, starting from the end, managing overflow into the next block
            for (int i = bitData.Length - 1; i >= extraLeadingBlocks; i--)
            {
                ulong currentBlock = bitData.Span[i];

                if (requiresOverflowToNewBlock)
                {
                    // Shift the overflow block
                    bitData.Span[i + 1] |= currentBlock >> (64 - blockBitShift);
                }

                // Shift the current block
                bitData.Span[i] = currentBlock << blockBitShift;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int requiredBitDataSize)
        {
            if (requiredBitDataSize >= bitData.Length)
            {
                // We need to allocate a new chunk
                var newBitData = BitBufferCache.Lease(requiredBitDataSize);

                // Copy the existing data
                bitData.Span.CopyTo(newBitData.Span);

                // Return the old chunk
                BitBufferCache.Return(bitData);

                // Set the new chunk
                bitData = newBitData;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong CalculateBit(int value, int index)
        {
            if (index == 0)
            {
                return (1UL << (value - baseOffset));
            }
            else
            {
                return (1UL << ((value - (index * 64)) - baseOffset));
            }
        }
    }
}
