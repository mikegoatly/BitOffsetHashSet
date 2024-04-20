using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Goatly.BitOffsetHashSets
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct DataBlock()
    {
        private const int DataBlockElementCount = 4;
        private const int MaxValueCapacity = (DataBlockElementCount * 64) - 1;

        private ulong data1;
        private ulong data2;
        private ulong data3;
        private ulong data4;

        public DataBlock(int initialValue)
            : this()
        {
            this.BaseOffset = initialValue.AlignTo256BitBoundary();
            this.MaxPossibleValue = this.BaseOffset + MaxValueCapacity;
        }

        public readonly int MaxPossibleValue { get; }

        public int BaseOffset { get; }

        public readonly bool HasData => this.data1 > 0 || this.data2 > 0 || this.data3 > 0 || this.data4 > 0;

        public readonly IEnumerable<int> EnumerateItems()
        {
            int offset = this.BaseOffset;

            return EnumerateItems(offset, this.data1)
                .Concat(EnumerateItems(offset + 64, this.data2))
                .Concat(EnumerateItems(offset + 128, this.data3))
                .Concat(EnumerateItems(offset + 192, this.data4));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Add(int value)
        {
            var relativeValue = this.CalculateRelativeValue(value);

            ref var dataSlot = ref this.CalculateSlot(relativeValue);
            var bit = CalculateBit(relativeValue);
            if ((dataSlot & bit) == 0)
            {
                dataSlot |= bit;
                return true;
            }

            return false;
        }



        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool Remove(int value)
        {
            var relativeValue = this.CalculateRelativeValue(value);

            ref var dataSlot = ref this.CalculateSlot(relativeValue);
            var bit = CalculateBit(relativeValue);
            if ((dataSlot & bit) != 0)
            {
                dataSlot &= ~bit;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal readonly bool Contains(int value)
        {
            var relativeValue = this.CalculateRelativeValue(value);

            ref var dataSlot = ref this.CalculateSlot(relativeValue);

            return (dataSlot & CalculateBit(relativeValue)) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static IEnumerable<int> EnumerateItems(int baseOffset, ulong data)
        {
            while (data != 0)
            {
                int bit = BitOperations.TrailingZeroCount(data);
                yield return baseOffset + bit;
                data &= data - 1;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly int CalculateRelativeValue(int value)
        {
            return value - this.BaseOffset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong CalculateBit(int relativeValue)
        {
            return 1UL << relativeValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly unsafe ref ulong CalculateSlot(int relativeValue)
        {
            fixed (ulong* ptr = &data1)
            {
                return ref ptr[relativeValue / 64];
            }
        }
    }

    public sealed class BitOffsetHashSet : IEnumerable<int>
    {
        private int count;
        private DataBlock[] dataBlocks;
        private int dataBlockCount;

        public BitOffsetHashSet(int initialCapacity = 1)
        {
            this.dataBlocks = new DataBlock[initialCapacity];
        }

        public BitOffsetHashSet(IEnumerable<int> values)
        {
            ArgumentNullException.ThrowIfNull(values);

            if (values is BitOffsetHashSet other)
            {
                this.count = other.count;
                this.dataBlockCount = other.dataBlockCount;
                this.dataBlocks = [.. other.dataBlocks];
            }
            else
            {
                // This is be optimisable - at the very least check to see if this implements ICollection and get a count,
                // and approximate an initial index size from that.
                this.dataBlocks = new DataBlock[1];

                foreach (var value in values)
                {
                    Add(value);
                }
            }
        }

        internal IList<DataBlock> DataBlocks => this.dataBlocks.Take(this.dataBlockCount).ToList();

        public int Count => count;

        public bool Add(int value)
        {
            ref var dataBlock = ref FindDataBlockForAddition(value);

            if (dataBlock.Add(value))
            {
                this.count++;
                return true;
            }

            return false;
        }

        private ref DataBlock FindDataBlockForAddition(int value)
        {
            // Binary search for the first block that can contain the value
            // If we can't find a block, we need to insert a new one in the correct place.
            int left = 0;
            var dataBlocks = this.dataBlocks;
            int right = dataBlockCount - 1;
            while (left <= right)
            {
                int mid = left + ((right - left) / 2);
                ref var dataBlock = ref dataBlocks[mid];

                if (value < dataBlock.BaseOffset)
                {
                    right = mid - 1;
                }
                else if (value > dataBlock.MaxPossibleValue)
                {
                    left = mid + 1;
                }
                else
                {
                    return ref dataBlock;
                }
            }

            // If we get here, the value is outside the range of all blocks.
            // We need to insert a new block at the correct position.
            this.InsertDataBlock(left, new DataBlock(value));
            return ref this.dataBlocks[left];
        }

        private unsafe void InsertDataBlock(int left, DataBlock newBlock)
        {
            if (this.dataBlocks.Length == this.dataBlockCount)
            {
                var newDataBlocks = new DataBlock[this.dataBlockCount * 2];

                if (left > 0)
                {
                    fixed (DataBlock* target = &newDataBlocks[0])
                    fixed (DataBlock* src = &this.dataBlocks[0])
                    {
                        var size = left * sizeof(DataBlock);
                        Buffer.MemoryCopy(src, target, size, size);
                    }
                }

                newDataBlocks[left] = newBlock;

                if (left < this.dataBlockCount)
                {
                    fixed (DataBlock* target = &newDataBlocks[left + 1])
                    fixed (DataBlock* src = &this.dataBlocks[left])
                    {
                        var size = (this.dataBlockCount - left) * sizeof(DataBlock);
                        Buffer.MemoryCopy(src, target, size, size);
                    }
                }

                this.dataBlocks = newDataBlocks;
            }
            else
            {
                if (left < this.dataBlockCount)
                {
                    fixed (DataBlock* src = &this.dataBlocks[left])
                    {
                        var size = (this.dataBlockCount - left) * sizeof(DataBlock);

                        // Buffer.MemoryCopy is safe to use with overlapping data: https://learn.microsoft.com/en-us/dotnet/api/system.buffer.memorycopy?view=net-8.0
                        Buffer.MemoryCopy(src, src + 1, size, size);
                    }
                }

                this.dataBlocks[left] = newBlock;
            }

            this.dataBlockCount++;
        }

        public bool Contains(int value)
        {
            var index = this.BinarySearchDataBlock(value);
            if (index >= 0)
            {
                ref var dataBlock = ref this.dataBlocks[index];
                return dataBlock.Contains(value);
            }

            return false;
        }

        public bool Remove(int value)
        {
            var index = this.BinarySearchDataBlock(value);
            if (index >= 0)
            {
                ref var dataBlock = ref dataBlocks[index];
                if (dataBlock.Remove(value))
                {
                    this.count--;
                    return true;
                }
            }

            return false;
        }

        private int BinarySearchDataBlock(int value)
        {
            // Binary search for the first block that can contain the value
            int left = 0;
            var dataBlocks = this.dataBlocks;
            int right = this.dataBlockCount - 1;
            while (left <= right)
            {
                int mid = left + ((right - left) / 2);
                ref var dataBlock = ref dataBlocks[mid];

                if (value < dataBlock.BaseOffset)
                {
                    right = mid - 1;
                }
                else if (value > dataBlock.MaxPossibleValue)
                {
                    left = mid + 1;
                }
                else
                {
                    return mid;
                }
            }

            // If we get here, the value is outside the range of all blocks.
            return -1;
        }

        public bool Compact()
        {
            if (count == 0)
            {
                this.dataBlocks = [];
                return true;
            }

            var initialSize = dataBlockCount;

            this.dataBlocks = this.dataBlocks
                .Take(this.dataBlockCount)
                .Where(x => x.HasData)
                .ToArray();

            this.dataBlockCount = this.dataBlocks.Length;

            return this.dataBlockCount < initialSize;
        }

        public IEnumerator<int> GetEnumerator()
        {
            return EnumerateItems().GetEnumerator();
        }

        private IEnumerable<int> EnumerateItems()
        {
            for (var i = 0; i < this.dataBlockCount; i++)
            {
                foreach (var item in this.dataBlocks[i].EnumerateItems())
                {
                    yield return item;
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Clear()
        {
            this.count = 0;
            this.dataBlockCount = 0;
        }
    }
}
