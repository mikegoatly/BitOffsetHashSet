using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Goatly.BitOffsetHashSets
{
    public sealed class BitOffsetHashSet : IEnumerable<int>
    {
        private int count;
        private int baseOffset;
        private ulong[] bitData;

        public BitOffsetHashSet(int initialCapacity = 1)
        {
            bitData = new ulong[initialCapacity];
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

            ref var slot = ref bitData[index];
            var bit = CalculateBit(value, index);
            var alreadySet = (slot & bit) != 0;
            if (!alreadySet)
            {
                slot |= bit;
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

            return (this.bitData[index] & CalculateBit(value, index)) != 0;
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
            var current = this.bitData[index];
            var alreadySet = (current & bit) != 0;
            if (alreadySet)
            {
                var newValue = current & ~bit;
                this.bitData[index] = newValue;
                this.count--;

                return true;
            }

            return false;
        }

        public bool Compact()
        {
            int lastNonZeroBlock = bitData.Length - 1;
            while (lastNonZeroBlock >= 0 && bitData[lastNonZeroBlock] == 0)
            {
                lastNonZeroBlock--;
            }

            if (lastNonZeroBlock == -1)
            {
                // The list is empty
                bitData = [];
                baseOffset = 0;
                count = 0;
                return true;
            }

            int firstNonZeroBlock = 0;
            while (firstNonZeroBlock < bitData.Length && bitData[firstNonZeroBlock] == 0)
            {
                firstNonZeroBlock++;
            }

            if (firstNonZeroBlock == 0 && lastNonZeroBlock == bitData.Length - 1)
            {
                // No need to compact
                return false;
            }

            // Request a new chunk of the right size
            var newBitData = bitData[firstNonZeroBlock..(lastNonZeroBlock + 1)];

            if (firstNonZeroBlock > 0)
            {
                // Adjust the base offset
                baseOffset += firstNonZeroBlock * 64;
            }

            var leadingZeroCount = BitOperations.LeadingZeroCount(newBitData[0]);
            if (leadingZeroCount > 0)
            {
                // Shift all the data to the left
                for (int i = 0; i < newBitData.Length - 1; i++)
                {
                    newBitData[i] = (newBitData[i] << leadingZeroCount) | (newBitData[i + 1] >> (64 - leadingZeroCount));
                }

                newBitData[^1] = newBitData[^1] << leadingZeroCount;

                // Adjust the base offset
                baseOffset -= leadingZeroCount;
            }

            bitData = newBitData;

            return true;
        }

        public IEnumerator<int> GetEnumerator()
        {
            int enumeratedCount = 0;
            int offset = baseOffset;
            int index = 0;
            while (enumeratedCount < count)
            {
                ulong current = bitData[index];
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
            Array.Clear(this.bitData);
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
            var requiresOverflowToNewBlock = BitOperations.LeadingZeroCount(bitData[^1]) < blockBitShift;

            // Determine if we need to allocate a new chunk
            if (requiresOverflowToNewBlock || extraLeadingBlocks > 0)
            {
                var newBitData = new ulong[bitData.Length + extraLeadingBlocks + (requiresOverflowToNewBlock ? 1 : 0)];
                Array.Copy(bitData, 0, newBitData, extraLeadingBlocks, bitData.Length);
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
                ulong currentBlock = bitData[i];

                if (requiresOverflowToNewBlock)
                {
                    // Shift the overflow block
                    bitData[i + 1] |= currentBlock >> (64 - blockBitShift);
                }

                // Shift the current block
                bitData[i] = currentBlock << blockBitShift;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int requiredBitDataSize)
        {
            if (requiredBitDataSize >= bitData.Length)
            {
                // We need to allocate a new chunk. If we're growing, we'll assume that's going to happen again soon
                // so we'll resize to double capacity, or requiredBitDataSize, whichever is larger
                var newBitData = new ulong[Math.Max(bitData.Length * 2, requiredBitDataSize)];

                // Copy the existing data
                Array.Copy(bitData, 0, newBitData, 0, bitData.Length);

                // Set the new chunk
                bitData = newBitData;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ulong CalculateBit(int value, int index)
        {
            return 1UL << (value - baseOffset - (index * 64));
        }
    }
}
