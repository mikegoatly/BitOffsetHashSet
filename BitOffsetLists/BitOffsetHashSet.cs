﻿using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
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

        public BitOffsetHashSet(IEnumerable<int> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (values is BitOffsetHashSet other)
            {
                this.baseOffset = other.baseOffset;
                this.count = other.count;
                this.bitData = [.. other.bitData];
            }
            else
            {
                if (values is ISet<int> set)
                {
                    (this.baseOffset, this.bitData) = DeriveDataStructures(set);

                    // With a set, we can assume that each value is unique, so the count will match
                    this.count = set.Count;
                    foreach (var value in set)
                    {
                        var index = (value - this.baseOffset) / 64;
                        this.bitData[index] |= CalculateBit(value, index);
                    }
                }
                else if (values is ICollection<int> collection)
                {
                    (this.baseOffset, this.bitData) = DeriveDataStructures(collection);

                    // We can't assume that each value will be unique in a collection,
                    // so we need to count them individually
                    ref var count = ref this.count;

                    foreach (var value in collection)
                    {
                        var index = (value - this.baseOffset) / 64;
                        ref var slot = ref bitData[index];
                        var bit = CalculateBit(value, index);
                        if ((slot & bit) == 0)
                        {
                            slot |= bit;
                            count++;
                        }
                    }
                }
                else
                {
                    // Last resort - just add each value individually
                    this.bitData = new ulong[1];

                    foreach (var value in values)
                    {
                        this.Add(value);
                    }
                }
            }
        }

        private (int baseOffset, ulong[] bitData) DeriveDataStructures(ICollection<int> collection)
        {
            // Find the min/max values to work out the base offset and 
            // appropriate size for the bit data buffer
            int min = int.MaxValue;
            int max = int.MinValue;
            foreach (var value in collection)
            {
                if (value < min)
                {
                    min = value;
                }

                if (value > max)
                {
                    max = value;
                }
            }

            return 
            (
                AlignTo64BitBoundary(min),
                new ulong[(max - this.baseOffset) / 64 + 1]
            );
        }

        internal int BaseOffset => baseOffset;

        internal int BitDataBufferLength => bitData.Length;

        public int Count => count;

        public bool Add(int value)
        {
            int index;
            if (count == 0)
            {
                index = 0;
                baseOffset = AlignTo64BitBoundary(value);
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
            ref var current = ref this.bitData[index];
            var alreadySet = (current & bit) != 0;
            if (alreadySet)
            {
                current &= ~bit;
                this.count--;

                return true;
            }

            return false;
        }

        public bool Compact()
        {
            if (count == 0)
            {
                // The list is empty
                bitData = [];
                baseOffset = 0;
                count = 0;
                return true;
            }

            int lastNonZeroBlock = bitData.Length - 1;
            while (lastNonZeroBlock >= 0 && bitData[lastNonZeroBlock] == 0)
            {
                lastNonZeroBlock--;
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
            newBaseOffset = AlignTo64BitBoundary(newBaseOffset);
            int totalBitShift = baseOffset - newBaseOffset;
            baseOffset = newBaseOffset;
            var extraLeadingBlocks = totalBitShift / 64;

            var newBitData = new ulong[bitData.Length + extraLeadingBlocks];
            Array.Copy(bitData, 0, newBitData, extraLeadingBlocks, bitData.Length);
            bitData = newBitData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int requiredBitDataSize)
        {
            if (requiredBitDataSize > bitData.Length)
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

        /// <summary>
        /// Aligns a value to the start of a 64 bit boundary.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int AlignTo64BitBoundary(int value)
        {
            return value & ~63;
        }
    }
}
