using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Goatly.BitOffsetHashSets
{
    internal record struct DataBlock(int BaseOffset, ulong[] Data)
    {
        public bool Contains(int value)
        {
            if (value < this.BaseOffset)
            {
                return false;
            }

            int index = (value - this.BaseOffset) / 64;
            if (index >= this.Data.Length)
            {
                return false;
            }

            return (this.Data[index] & CalculateBit(value, index)) != 0;
        }

        public bool Remove(int value)
        {
            if (value < this.BaseOffset)
            {
                return false;
            }

            int index = (value - this.BaseOffset) / 64;
            if (index >= this.Data.Length)
            {
                return false;
            }

            var bit = CalculateBit(value, index);
            ref var current = ref this.Data[index];
            var alreadySet = (current & bit) != 0;
            if (alreadySet)
            {
                current &= ~bit;
                return true;
            }

            return false;
        }

        public void Clear()
        {
            Array.Clear(this.Data);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly ulong CalculateBit(int value, int index)
        {
            return 1UL << (value - this.BaseOffset - (index * 64));
        }

        public readonly DataBlock ShiftBaseTo(int newBaseOffset)
        {
            if (newBaseOffset >= this.BaseOffset)
            {
                throw new ArgumentOutOfRangeException(nameof(newBaseOffset), "The new base offset must be less than the current base offset");
            }

            newBaseOffset = newBaseOffset.AlignTo64BitBoundary();
            int totalBitShift = this.BaseOffset - newBaseOffset;
            var extraLeadingBlocks = totalBitShift / 64;

            var data = this.Data;
            var newBitData = new ulong[data.Length + extraLeadingBlocks];
            Array.Copy(data, 0, newBitData, extraLeadingBlocks, data.Length);
            return new DataBlock(newBaseOffset, newBitData);
        }
    }

    public sealed class BitOffsetHashSet : IEnumerable<int>
    {
        private int count;
        private DataBlock dataBlock;

        public BitOffsetHashSet(int initialCapacity = 1)
        {
            count = 0;
            dataBlock = new DataBlock(0, new ulong[initialCapacity]);
        }

        public BitOffsetHashSet(IEnumerable<int> values)
        {
            ArgumentNullException.ThrowIfNull(values);

            if (values is BitOffsetHashSet other)
            {
                count = other.count;
                this.dataBlock = other.dataBlock;
            }
            else
            {
                if (values is ISet<int> set)
                {
                    this.dataBlock = DeriveDataStructures(set);
                    var (baseOffset, data) = this.dataBlock;

                    // With a set, we can assume that each value is unique, so the count will match
                    this.count = set.Count;
                    foreach (var value in set)
                    {
                        var index = (value - baseOffset) / 64;
                        data[index] |= this.dataBlock.CalculateBit(value, index);
                    }
                }
                else if (values is ICollection<int> collection)
                {
                    this.dataBlock = DeriveDataStructures(collection);

                    // We can't assume that each value will be unique in a collection,
                    // so we need to count them individually
                    ref var count = ref this.count;
                    foreach (var value in collection)
                    {
                        var index = (value - this.dataBlock.BaseOffset) / 64;
                        ref var slot = ref dataBlock.Data[index];
                        var bit = this.dataBlock.CalculateBit(value, index);
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
                    this.dataBlock = new DataBlock(0, new ulong[1]);

                    foreach (var value in values)
                    {
                        Add(value);
                    }
                }
            }
        }

        private DataBlock DeriveDataStructures(ICollection<int> collection)
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

            var baseOffset = min.AlignTo64BitBoundary();
            return new DataBlock(
                baseOffset,
                new ulong[(max - baseOffset) / 64 + 1]);
        }

        internal int BaseOffset => dataBlock.BaseOffset;

        internal int BitDataBufferLength => dataBlock.Data.Length;

        public int Count => count;

        public bool Add(int value)
        {
            int index;
            if (count == 0)
            {
                index = 0;
                this.dataBlock = this.dataBlock with { BaseOffset = value.AlignTo64BitBoundary() };
            }
            else
            {
                if ((value < dataBlock.BaseOffset))
                {
                    this.dataBlock = this.dataBlock.ShiftBaseTo(value);
                }

                index = (value - dataBlock.BaseOffset) / 64;
            }

            EnsureCapacity(index + 1);

            ref var slot = ref dataBlock.Data[index];
            var bit = this.dataBlock.CalculateBit(value, index);
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
            return this.dataBlock.Contains(value);
        }

        public bool Remove(int value)
        {
            var removed = this.dataBlock.Remove(value);
            if (removed)
            {
                this.count--;
            }

            return removed;
        }

        public bool Compact()
        {
            if (count == 0)
            {
                dataBlock = new DataBlock(0, []);
                return true;
            }

            var (_, data) = dataBlock;
            var length = data.Length;
            int lastNonZeroBlock = length - 1;
            while (lastNonZeroBlock >= 0 && data[lastNonZeroBlock] == 0)
            {
                lastNonZeroBlock--;
            }

            int firstNonZeroBlock = 0;
            while (firstNonZeroBlock < length && data[firstNonZeroBlock] == 0)
            {
                firstNonZeroBlock++;
            }

            if (firstNonZeroBlock == 0 && lastNonZeroBlock == length - 1)
            {
                // No need to compact
                return false;
            }

            // Request a new chunk of the right size
            var newBitData = data[firstNonZeroBlock..(lastNonZeroBlock + 1)];

            if (firstNonZeroBlock > 0)
            {
                this.dataBlock = new DataBlock(dataBlock.BaseOffset + firstNonZeroBlock * 64, newBitData);
            }
            else
            {
                this.dataBlock = this.dataBlock with { Data = newBitData };
            }

            return true;
        }

        public IEnumerator<int> GetEnumerator()
        {
            int enumeratedCount = 0;
            int offset = dataBlock.BaseOffset;
            int index = 0;
            while (enumeratedCount < count)
            {
                ulong current = dataBlock.Data[index];
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
            return GetEnumerator();
        }

        public void Clear()
        {
            count = 0;
            dataBlock.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureCapacity(int requiredBitDataSize)
        {
            if (requiredBitDataSize > dataBlock.Data.Length)
            {
                var newBitData = new ulong[Math.Max(dataBlock.Data.Length * 2, requiredBitDataSize)];
                Array.Copy(dataBlock.Data, 0, newBitData, 0, dataBlock.Data.Length);
                dataBlock = new DataBlock(dataBlock.BaseOffset, newBitData);
            }
        }
    }
}
