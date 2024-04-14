using System.Collections;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Goatly.BitOffsetHashSets
{
    internal record struct DataBlock()
    {
        public DataBlock(int baseOffset, ulong[] data)
            : this()
        {
            this.BaseOffset = baseOffset;
            this.Data = data;
            this.MaxPossibleValue = baseOffset + (data.Length * 64) - 1;
        }

        public DataBlock(DataBlock other)
            : this()
        {
            this.BaseOffset = other.BaseOffset;
            this.Data = [.. other.Data];
            this.MaxPossibleValue = other.MaxPossibleValue;
        }

        public int MaxPossibleValue { get; }
        public int BaseOffset { get; }
        public ulong[] Data { get; }

        public readonly bool Contains(int value)
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

        public readonly bool Remove(int value)
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void Clear()
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
        internal const int DefaultInitialCapacity = 4;

        /// <summary>
        /// To avoid big empty gaps in the data, we'll add a new block when the gap between a new value and existing values
        /// is greater than this.
        /// </summary>
        internal const int SparseGap = 64 * 10;
        private readonly int initialCapacity = DefaultInitialCapacity;

        private int count;
        private DataBlock[] dataBlocks = new DataBlock[1];

        public BitOffsetHashSet(int initialCapacity = DefaultInitialCapacity)
        {
            count = 0;
            this.initialCapacity = initialCapacity;
            this.dataBlocks[0] = new DataBlock(0, new ulong[initialCapacity]);
        }

        public BitOffsetHashSet(IEnumerable<int> values)
        {
            ArgumentNullException.ThrowIfNull(values);

            if (values is BitOffsetHashSet other)
            {
                count = other.count;
                this.dataBlocks = other.dataBlocks.Select(b => new DataBlock(b)).ToArray();
            }
            else
            {
                // This may be optimisable.
                this.dataBlocks[0] = new DataBlock(0, new ulong[initialCapacity]);
                foreach (var value in values)
                {
                    Add(value);
                }
            }
        }

        internal DataBlock CoreBlock => this.dataBlocks[0];
        internal List<DataBlock>? SparseBlocks => this.dataBlocks.Length > 1 ? [.. this.dataBlocks[1..]] : null;

        public int Count => count;

        public bool Add(int value)
        {
            if (count == 0)
            {
                InitializeFirstEntry(value);
                return true;
            }

            var dataBlock = FindDataBlockForAddition(value);

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

        private void InitializeFirstEntry(int value)
        {
            ref var dataBlock = ref this.dataBlocks[0];
            dataBlock = new DataBlock(value.AlignTo64BitBoundary(), dataBlock.Data);
            dataBlock.Data[0] = dataBlock.CalculateBit(value, 0);
            this.count = 1;
        }

        private DataBlock FindDataBlockForAddition(int value)
        {
            // Look for the first block that can contain the value
            for (int i = 0; i < this.dataBlocks.Length; i++)
            {
                // We get a ref to the block because at some point we may need to mutate it
                ref var dataBlock = ref this.dataBlocks[i];

                if (value < dataBlock.BaseOffset)
                {
                    if (value < dataBlock.BaseOffset - SparseGap)
                    {
                        // In this case we need to insert a new block before this one
                        // TODO - initial size?
                        var insertedBlock = new DataBlock(value.AlignTo64BitBoundary(), new ulong[initialCapacity]);
                        this.dataBlocks = [.. this.dataBlocks[0..i], insertedBlock, .. this.dataBlocks[i..]];
                        return insertedBlock;
                    }

                    // Shift the base offset of this block
                    dataBlock = dataBlock.ShiftBaseTo(value);
                    return dataBlock;
                }

                if (value <= dataBlock.MaxPossibleValue)
                {
                    return dataBlock;
                }

                // The maximum possible value this block can contain is block.MaxPossibleValue + SparseGap, OR the min - 1 of the next block
                var maxExpandableValue = Math.Min(
                    dataBlock.MaxPossibleValue + SparseGap,
                    i == this.dataBlocks.Length - 1 ? int.MaxValue : this.dataBlocks[i + 1].BaseOffset - 1);

                if (value >= dataBlock.BaseOffset && value <= maxExpandableValue)
                {
                    // This block fits, expand if necessary
                    EnsureCapacity(ref dataBlock, value, maxExpandableValue);
                    return dataBlock;
                }
            }

            // No block found, create a new one
            var newBlock = new DataBlock(value.AlignTo64BitBoundary(), new ulong[initialCapacity]);
            this.dataBlocks = [.. this.dataBlocks, newBlock];
            return newBlock;
        }

        public bool Contains(int value)
        {
            if (FindDataBlockForValue(value, out var dataBlock))
            {
                return dataBlock.Contains(value);
            }

            return false;
        }

        private bool FindDataBlockForValue(int value, out DataBlock dataBlock)
        {
            // Find the first block for which the value is less than the max value. 
            for (int i = 0; i < this.dataBlocks.Length; i++)
            {
                dataBlock = this.dataBlocks[i];
                if (value <= dataBlock.MaxPossibleValue)
                {
                    return true;
                }
            }

            dataBlock = default;
            return false;
        }

        public bool Remove(int value)
        {
            if (FindDataBlockForValue(value, out var dataBlock))
            {
                var removed = dataBlock.Remove(value);
                if (removed)
                {
                    this.count--;
                }

                return removed;
            }

            return false;
        }

        public bool Compact()
        {
            if (count == 0)
            {
                this.dataBlocks = [new DataBlock(0, new ulong[initialCapacity])];
                return true;
            }

            var compacted = false;
            this.dataBlocks = this.dataBlocks
                .Select(Compact)
                .Where(static result => result.IsEmpty == false)
                .Select(result =>
                {
                    compacted |= result.IsCompacted;
                    return result.DataBlock;
                })
                .ToArray();


            return compacted;
        }

        private static CompactionResult Compact(DataBlock dataBlock)
        {
            var data = dataBlock.Data;
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

            if (firstNonZeroBlock == length)
            {
                // The block is empty
                return new CompactionResult(dataBlock, false, true);
            }

            if (firstNonZeroBlock == 0 && lastNonZeroBlock == length - 1)
            {
                // No need to compact
                return new CompactionResult(dataBlock, false, false);
            }

            // Request a new chunk of the right size
            var newBitData = data[firstNonZeroBlock..(lastNonZeroBlock + 1)];

            if (firstNonZeroBlock > 0)
            {
                dataBlock = new DataBlock(dataBlock.BaseOffset + firstNonZeroBlock * 64, newBitData);
            }
            else
            {
                dataBlock = new DataBlock(dataBlock.BaseOffset, newBitData);
            }

            return new CompactionResult(dataBlock, true, false);
        }

        private record CompactionResult(DataBlock DataBlock, bool IsCompacted, bool IsEmpty);

        public IEnumerator<int> GetEnumerator()
        {
            return EnumerateItems().GetEnumerator();
        }

        private IEnumerable<int> EnumerateItems()
        {
            foreach (var dataBlock in this.dataBlocks)
            {
                foreach (var item in dataBlock.EnumerateItems())
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

            // We need to reset to just a single data block here, otherwise we will
            // confuse the initialisation process when adding the first element again.
            var dataBlock = this.dataBlocks[0];
            this.dataBlocks = [dataBlock];
            dataBlock.Clear();
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
#if DEBUG
                    if (requiredBlockCount > maxBlockCount)
                    {
                        // This *shouldn't* happen - guards in other logic should be making sure of it.
                        throw new InvalidOperationException("Unexpected attempt to grow a data block into a neighboring sparse block.");
                    }
#endif

                    newBlockCount = requiredBlockCount;
                }

                var newBitData = new ulong[newBlockCount];
                Array.Copy(dataBlock.Data, 0, newBitData, 0, length);
                dataBlock = new DataBlock(dataBlock.BaseOffset, newBitData);
            }
        }
    }
}
