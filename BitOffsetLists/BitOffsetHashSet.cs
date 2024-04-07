using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Goatly.BitOffsetHashSets
{
    internal record struct DataBlock(int BaseOffset, ulong[] Data)
    {
        public int MaxPossibleValue => this.BaseOffset + (this.Data.Length * 64) - 1;

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

        public readonly IEnumerable<int> EnumerateItems()
        {
            int offset = this.BaseOffset;
            int index = 0;

            while (index < this.Data.Length)
            {
                ulong current = this.Data[index];
                while (current != 0)
                {
                    int bit = BitOperations.TrailingZeroCount(current);
                    yield return offset + bit;
                    current &= current - 1;
                }

                offset += 64;
                index++;
            }
        }
    }

    public sealed class BitOffsetHashSet : IEnumerable<int>
    {
        /// <summary>
        /// To avoid big empty gaps in the data, we'll add a new sparse block when the gap between a new value and existing values
        /// is greater than this.
        /// </summary>
        internal const int SparseGap = 64 * 10;
        private const int InitialCapacity = 1;

        private int count;
        private DataBlock dataBlock;
        private List<DataBlock>? sparseBlocks;

        public BitOffsetHashSet(int initialCapacity = InitialCapacity)
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
                this.dataBlock = new DataBlock { BaseOffset = other.dataBlock.BaseOffset, Data = [.. other.dataBlock.Data] };
                // TODO Test cloning with sparse blocks
                this.sparseBlocks = other.sparseBlocks is null ? null : new List<DataBlock>(other.sparseBlocks);
            }
            else
            {
                // This may be optimisable.
                this.dataBlock = new DataBlock(0, new ulong[InitialCapacity]);
                foreach (var value in values)
                {
                    Add(value);
                }
            }
        }

        internal DataBlock CoreBlock => this.dataBlock;
        internal List<DataBlock>? SparseBlocks => this.sparseBlocks;

        public int Count => count;

        public bool Add(int value)
        {
            if (count == 0)
            {
                this.dataBlock = this.dataBlock with { BaseOffset = value.AlignTo64BitBoundary() };
                this.dataBlock.Data[0] = this.dataBlock.CalculateBit(value, 0);
                this.count = 1;
                return true;
            }

            ref var dataBlock = ref FindDataBlockForAddition(value);

            var index = (value - dataBlock.BaseOffset) / 64;
            ref var slot = ref dataBlock.Data[index];
            var bit = dataBlock.CalculateBit(value, index);
            var alreadySet = (slot & bit) != 0;
            if (!alreadySet)
            {
                slot |= bit;
                this.count++;
                return true;
            }

            return false;
        }

        private ref DataBlock FindDataBlockForAddition(int value)
        {
            // The core datablock will contain the lower bound of the values, so if the value is lower than that
            // we either need to shift the core block or create a new block and move this one to the sparse list
            if ((value < this.dataBlock.BaseOffset))
            {
                var lowerGap = dataBlock.BaseOffset - value;
                if (lowerGap > SparseGap)
                {
                    // Move the current core block to the sparse list
                    this.sparseBlocks ??= [this.dataBlock];

                    // TODO - initial size?
                    this.dataBlock = new DataBlock(value.AlignTo64BitBoundary(), new ulong[InitialCapacity]);
                    return ref this.dataBlock;
                }

                // Just shift the core block
                this.dataBlock = this.dataBlock.ShiftBaseTo(value);
                return ref this.dataBlock;
            }

            var maxExpandableValue = Math.Min(
                this.dataBlock.MaxPossibleValue + SparseGap,
                this.sparseBlocks is null ? int.MaxValue : this.sparseBlocks[0].BaseOffset - 1);

            if (value > maxExpandableValue)
            {
                if (this.sparseBlocks is null)
                {
                    // Just create the sparse blocks with a new block to fit the value
                    // TODO - initial size?
                    this.sparseBlocks = [new DataBlock(value.AlignTo64BitBoundary(), new ulong[InitialCapacity])];
                    return ref CollectionsMarshal.AsSpan(this.sparseBlocks)[0];
                }

                // Look for a sparse block that can contain the value
                var collectionSpan = CollectionsMarshal.AsSpan(this.sparseBlocks);
                for (int i = 0; i < collectionSpan.Length; i++)
                {
                    ref DataBlock block = ref collectionSpan[i];

                    if (value < block.BaseOffset)
                    {
                        if (value < block.BaseOffset - SparseGap)
                        {
                            // In this case we need to insert a new block before this one
                            // TODO - initial size?
                            this.sparseBlocks.Insert(i, new DataBlock(value.AlignTo64BitBoundary(), new ulong[InitialCapacity]));
                            return ref CollectionsMarshal.AsSpan(this.sparseBlocks)[i];
                        }

                        // Shift the base offset of this block
                        block = block.ShiftBaseTo(value);
                        return ref block;
                    }

                    // The maximum possible value this block can contain is block.MaxPossibleValue + SparseGap, OR the min - 1 of the next block
                    maxExpandableValue = Math.Min(
                        block.MaxPossibleValue + SparseGap,
                        i == collectionSpan.Length - 1 ? int.MaxValue : collectionSpan[i + 1].BaseOffset - 1);

                    if (value >= block.BaseOffset && value <= maxExpandableValue)
                    {
                        // This block fits, expand if necessary
                        EnsureCapacity(ref block, value, maxExpandableValue);
                        return ref block;
                    }
                }

                // No block found, create a new one
                // TODO - initial size?
                this.sparseBlocks.Add(new DataBlock(value.AlignTo64BitBoundary(), new ulong[InitialCapacity]));
                return ref CollectionsMarshal.AsSpan(this.sparseBlocks)[^1];

            }

            // Just use the core block, expanding as necessary
            EnsureCapacity(ref this.dataBlock, value, maxExpandableValue);
            return ref this.dataBlock;
        }

        public bool Contains(int value)
        {
            ref var dataBlock = ref FindDataBlockForValue(value);
            return dataBlock.Contains(value);
        }

        private ref DataBlock FindDataBlockForValue(int value)
        {
            if (this.sparseBlocks == null)
            {
                return ref this.dataBlock;
            }

            if (value > this.dataBlock.MaxPossibleValue)
            {
                var collectionSpan = CollectionsMarshal.AsSpan(this.sparseBlocks);
                for (int i = 0; i < collectionSpan.Length; i++)
                {
                    ref DataBlock block = ref collectionSpan[i];
                    if (value >= block.BaseOffset && value <= block.MaxPossibleValue)
                    {
                        return ref block;
                    }
                }

                return ref this.dataBlock;
            }

            return ref this.dataBlock;
        }

        public bool Remove(int value)
        {
            ref var dataBlock = ref FindDataBlockForValue(value);

            var removed = dataBlock.Remove(value);
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
            return EnumerateItems().GetEnumerator();
        }

        private IEnumerable<int> EnumerateItems()
        {
            var dataBlock = this.dataBlock;

            foreach (var item in dataBlock.EnumerateItems())
            {
                yield return item;
            }

            if (this.sparseBlocks is not null)
            {
                foreach (var sparseBlock in this.sparseBlocks)
                {
                    foreach (var item in sparseBlock.EnumerateItems())
                    {
                        yield return item;
                    }
                }
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

            if (this.sparseBlocks is not null)
            {
                foreach (var block in this.sparseBlocks)
                {
                    block.Clear();
                }
            }
        }

        private void EnsureCapacity(ref DataBlock dataBlock, int value, int maxExpandableValue)
        {
            var requiredBlockCount = ((value - dataBlock.BaseOffset) / 64) + 1;

            var length = dataBlock.Data.Length;
            if (requiredBlockCount > length)
            {
                // Calculate the length of the block, ensuring no overlap with the next expansion block (if there is one)
                var maxBlockCount = ((maxExpandableValue - dataBlock.BaseOffset) + 1) / 64;

                // We'll either double the length of the block, or just fill up to the next sparse block, whichever is smallest
                var newBlockCount = Math.Min(length * 2, maxBlockCount);

                // But we also need to ensure it's *at least* the required block count.
                if (requiredBlockCount > newBlockCount)
                {
                    if (requiredBlockCount > maxBlockCount)
                    {
                        // This *shouldn't* happen - guards in other logic should be making sure of it.
                        throw new InvalidOperationException("Unexpected attempt to grow a data block into a neighboring sparse block.");
                    }

                    newBlockCount = requiredBlockCount;
                }

                var newBitData = new ulong[newBlockCount];
                Array.Copy(dataBlock.Data, 0, newBitData, 0, length);
                dataBlock = dataBlock with { Data = newBitData };
            }
        }
    }
}
