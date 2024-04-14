namespace Goatly.BitOffsetHashSets.Tests.Unit
{
    public class BitOffsetHashSetTests
    {
        private BitOffsetHashSet sut;

        public BitOffsetHashSetTests()
        {
            this.sut = new BitOffsetHashSet(1);
        }

        [Fact]
        public void AddingIntoEmptyList()
        {
            Assert.Single(this.sut.CoreBlock.Data);

            Assert.True(this.sut.Add(100));

            Assert.Single(this.sut.CoreBlock.Data);
            Assert.Equal(1, this.sut.Count);

            Assert.True(this.sut.Contains(100));

            Assert.False(this.sut.Contains(99));
            Assert.False(this.sut.Contains(101));

            Assert.True(this.sut.ToList().SequenceEqual([100]));
        }

        [Fact]
        public void ConstructingFromExisting()
        {
            var original = new BitOffsetHashSet(1);

            original.Add(100);
            original.Add(101);

            // Push the block size to 2
            original.Add(140);
            original.Remove(140);

            this.sut = new BitOffsetHashSet(original);

            Assert.True(original.Contains(100));
            Assert.True(this.sut.Contains(101));

            Assert.Equal(64, this.sut.CoreBlock.BaseOffset);

            Assert.True(this.sut.ToList().SequenceEqual([100, 101]));

            // If we had done an enumeration copy, the buffer length would be 1 again
            Assert.Equal(2, this.sut.CoreBlock.Data.Length);
        }

        [Fact]
        public void ConstructingFromList()
        {
            this.sut = new BitOffsetHashSet(
                [
                    100, 150, 300
                ]);

            Assert.Equal(3, this.sut.Count);
            Assert.Equal(64, this.sut.CoreBlock.BaseOffset);
            Assert.True(this.sut.ToList().SequenceEqual([100, 150, 300]));
        }

        [Fact]
        public void ConstructingFromHashSet()
        {
            var hashSet = new HashSet<int>() { 100, 150, 300 };
            this.sut = new BitOffsetHashSet(hashSet);

            Assert.Equal(3, this.sut.Count);
            Assert.Equal(64, this.sut.CoreBlock.BaseOffset);
            Assert.True(this.sut.ToList().SequenceEqual([100, 150, 300]));
        }

        [Fact]
        public void ConstructingFromListWithDuplicates()
        {
            this.sut = new BitOffsetHashSet(
                [
                    300, 100, 150, 300, 100, 150
                ]);

            Assert.Equal(3, this.sut.Count);
            Assert.Equal(64, this.sut.CoreBlock.BaseOffset);
            Assert.True(this.sut.ToList().SequenceEqual([100, 150, 300]));
        }

        [Fact]
        public void ConstructingFromEnumerable()
        {
            static IEnumerable<int> Generator()
            {
                yield return 100;
                yield return 150;
                yield return 300;
            }

            this.sut = new BitOffsetHashSet(Generator());

            Assert.Equal(3, this.sut.Count);
            Assert.Equal(64, this.sut.CoreBlock.BaseOffset);
            Assert.True(this.sut.ToList().SequenceEqual([100, 150, 300]));
        }

        [Fact]
        public void ConstructingFromEnumerableWithDuplicates()
        {
            static IEnumerable<int> Generator()
            {
                yield return 300;
                yield return 100;
                yield return 150;
                yield return 300;
                yield return 100;
                yield return 150;
            }

            this.sut = new BitOffsetHashSet(Generator());

            Assert.Equal(3, this.sut.Count);
            Assert.Equal(64, this.sut.CoreBlock.BaseOffset);
            Assert.True(this.sut.ToList().SequenceEqual([100, 150, 300]));
        }

        [Fact]
        public void AddingIntoNonEmptyList_Within64()
        {
            this.sut.Add(100);

            Assert.True(this.sut.Add(101));

            Assert.Equal(2, this.sut.Count);

            Assert.True(this.sut.Contains(100));
            Assert.True(this.sut.Contains(101));

            Assert.False(this.sut.Contains(99));
            Assert.False(this.sut.Contains(102));

            Assert.True(this.sut.ToList().SequenceEqual([100, 101]));
        }

        [Fact]
        public void AddingIntoNonEmptyList_GreaterThan64()
        {
            this.sut.Add(100);

            Assert.True(this.sut.Add(250));

            Assert.Equal(2, this.sut.Count);

            Assert.True(this.sut.Contains(100));
            Assert.True(this.sut.Contains(250));

            Assert.True(this.sut.ToList().SequenceEqual([100, 250]));
        }

        [Fact]
        public void Adding_FirstSparseBlock()
        {
            this.sut.Add(100);

            Assert.True(this.sut.Add(25000));

            Assert.Equal(2, this.sut.Count);

            Assert.True(this.sut.Contains(100));
            Assert.True(this.sut.Contains(25000));

            Assert.True(this.sut.ToList().SequenceEqual([100, 25000]));

            Assert.Equal(64, this.sut.CoreBlock.BaseOffset);
            Assert.Single(this.sut.CoreBlock.Data);

            Assert.NotNull(this.sut.SparseBlocks);
            Assert.Single(this.sut.SparseBlocks);
            Assert.Equal(24960, this.sut.SparseBlocks[0].BaseOffset);
        }

        [Fact]
        public void Adding_IntoExistingSparseBlock()
        {
            this.sut.Add(100);

            Assert.True(this.sut.Add(25000));
            Assert.True(this.sut.Add(25002));

            Assert.Equal(3, this.sut.Count);

            Assert.True(this.sut.ToList().SequenceEqual([100, 25000, 25002]));

            Assert.NotNull(this.sut.SparseBlocks);
            Assert.Single(this.sut.SparseBlocks);
            Assert.Equal(24960, this.sut.SparseBlocks[0].BaseOffset);
        }

        [Fact]
        public void Adding_RebasingExistingSparseBlock()
        {
            this.sut.Add(100);

            Assert.True(this.sut.Add(25000));
            Assert.True(this.sut.Add(24900));

            Assert.Equal(3, this.sut.Count);

            Assert.True(this.sut.ToList().SequenceEqual([100, 24900, 25000]));

            Assert.NotNull(this.sut.SparseBlocks);
            Assert.Single(this.sut.SparseBlocks);
            Assert.Equal(24896, this.sut.SparseBlocks[0].BaseOffset);
            Assert.Equal(2, this.sut.SparseBlocks[0].Data.Length);
        }

        [Fact]
        public void Adding_DoesNotGrowCoreBlockToOverlapSparseBlock()
        {
            this.sut.Add(100);

            // Create the sparse block *just* outside the range of the core block
            var sparseBlockBase = 128 + BitOffsetHashSet.SparseGap;
            Assert.True(this.sut.Add(sparseBlockBase));

            Assert.Equal(2, this.sut.Count);

            Assert.True(this.sut.ToList().SequenceEqual([100, sparseBlockBase]));

            Assert.NotNull(this.sut.SparseBlocks);
            Assert.Single(this.sut.SparseBlocks);
            Assert.Equal(768, this.sut.SparseBlocks[0].BaseOffset);
            Assert.Single(this.sut.SparseBlocks[0].Data);

            // Now work our way up from the core block into the sparse block
            for (var i = 101; i < sparseBlockBase + 10; i++)
            {
                if (i == sparseBlockBase)
                {
                    Assert.False(this.sut.Add(i));
                }
                else
                {
                    Assert.True(this.sut.Add(i));
                }
            }

            Assert.Equal(678, this.sut.Count);

            // The core block should have grown to accommodate the new values, apart from the ones that end in the sparse block
            Assert.Equal(11, this.sut.CoreBlock.Data.Length);
            Assert.Single(this.sut.SparseBlocks);
            Assert.Single(this.sut.SparseBlocks[0].Data);

            // The core block should have grown to accommodate the new values, apart from the ones that end in the sparse block
            Assert.Equal(this.sut.CoreBlock.MaxPossibleValue, this.sut.SparseBlocks[0].BaseOffset - 1);
        }

        [Fact]
        public void Adding_ExtendingExistingSparseBlock()
        {
            this.sut.Add(100);

            Assert.True(this.sut.Add(25000));
            Assert.True(this.sut.Add(25064));

            Assert.Equal(3, this.sut.Count);

            Assert.True(this.sut.ToList().SequenceEqual([100, 25000, 25064]));

            Assert.NotNull(this.sut.SparseBlocks);
            Assert.Single(this.sut.SparseBlocks);
            Assert.Equal(24960, this.sut.SparseBlocks[0].BaseOffset);
            Assert.Equal(2, this.sut.SparseBlocks[0].Data.Length);
        }

        [Fact]
        public void Adding_IntoNewGreaterSparseBlock()
        {
            this.sut.Add(100);

            Assert.True(this.sut.Add(25000));
            Assert.True(this.sut.Add(350000));

            Assert.Equal(3, this.sut.Count);

            Assert.True(this.sut.ToList().SequenceEqual([100, 25000, 350000]));

            Assert.NotNull(this.sut.SparseBlocks);
            Assert.Equal(2, this.sut.SparseBlocks.Count);
            Assert.Equal(24960, this.sut.SparseBlocks[0].BaseOffset);
            Assert.Equal(349952, this.sut.SparseBlocks[1].BaseOffset);
        }

        [Fact]
        public void Adding_IntoNewLowerSparseBlock()
        {
            this.sut.Add(100);

            Assert.True(this.sut.Add(25000));
            Assert.True(this.sut.Add(10000));

            Assert.Equal(3, this.sut.Count);

            Assert.True(this.sut.ToList().SequenceEqual([100, 10000, 25000]));

            Assert.NotNull(this.sut.SparseBlocks);
            Assert.Equal(2, this.sut.SparseBlocks.Count);
            Assert.Equal(9984, this.sut.SparseBlocks[0].BaseOffset);
            Assert.Equal(24960, this.sut.SparseBlocks[1].BaseOffset);
        }


        [Fact]
        public void AddingWithLowerBaseValue_Within64()
        {
            this.sut.Add(100);

            Assert.True(this.sut.Add(99));

            Assert.Equal(2, this.sut.Count);

            Assert.True(this.sut.ToList().SequenceEqual([99, 100]));

            Assert.True(this.sut.Contains(99));
            Assert.True(this.sut.Contains(100));
        }

        [Fact]
        public void AddingWithLowerBaseValue_ExactMultipleOf64()
        {
            // Verifies we can add new elements to the start of the bit buffer without a bit shift
            this.sut.Add(1000);

            Assert.True(this.sut.Add(936));
            Assert.True(this.sut.Add(872));

            Assert.Equal(3, this.sut.Count);

            Assert.True(this.sut.ToList().SequenceEqual([872, 936, 1000]));

            Assert.True(this.sut.Contains(872));
            Assert.True(this.sut.Contains(936));
            Assert.True(this.sut.Contains(1000));
        }


        [Fact]
        public void AddingWithLowerBaseValue_NonMultipleOf64()
        {
            // Verifies we can add new elements to the end if we need to bit shift
            this.sut.Add(100);

            Assert.True(this.sut.Add(37));

            Assert.Equal(2, this.sut.Count);

            Assert.True(this.sut.ToList().SequenceEqual([37, 100]));

            Assert.True(this.sut.Contains(37));
            Assert.True(this.sut.Contains(100));
        }

        [Fact]
        public void EnumeratingEmptyList()
        {
            Assert.Empty(this.sut);
        }

        [Fact]
        public void RemovingFromEmptyList()
        {
            Assert.False(this.sut.Remove(100));

            Assert.Equal(0, this.sut.Count);
        }

        [Fact]
        public void RemovingExistingItemFromNonEmptyList()
        {
            this.sut.Add(100);

            Assert.True(this.sut.Remove(100));

            Assert.Equal(0, this.sut.Count);
        }

        [Fact]
        public void RemovingNonExistingItemFromNonEmptyList()
        {
            this.sut.Add(100);

            Assert.False(this.sut.Remove(99));

            Assert.Equal(1, this.sut.Count);
        }

        [Fact]
        public void RemovingFromSparseListShouldNotShrinkInternalBuffer()
        {
            this.sut.Add(100);
            this.sut.Add(350);

            Assert.Equal(5, this.sut.CoreBlock.Data.Length);

            Assert.True(this.sut.Remove(350));

            Assert.Equal(5, this.sut.CoreBlock.Data.Length);

            Assert.Equal(1, this.sut.Count);

            Assert.True(this.sut.Contains(100));
            Assert.False(this.sut.Contains(350));
            Assert.True(this.sut.ToList().SequenceEqual([100]));
        }

        [Fact]
        public void CompactingShouldRemoveDeadSpaceFromStartAndEndOfBuffer()
        {
            this.sut.Add(100);
            this.sut.Add(350);
            this.sut.Add(500);

            Assert.Equal(10, this.sut.CoreBlock.Data.Length);

            this.sut.Remove(100);
            this.sut.Remove(500);

            Assert.Equal(10, this.sut.CoreBlock.Data.Length);

            this.sut.Compact();

            Assert.Single(this.sut.CoreBlock.Data);

            Assert.True(this.sut.ToList().SequenceEqual([350]));
        }

        [Fact]
        public void CompactingShouldRemoveEmptyAdditionalBlocks()
        {
            this.sut.Add(100);
            this.sut.Add(3500);
            this.sut.Add(500);

            Assert.Single(this.sut.SparseBlocks!);

            this.sut.Remove(100);
            this.sut.Remove(3500);

            Assert.Equal(7, this.sut.CoreBlock.Data.Length);
            Assert.Single(this.sut.SparseBlocks!);

            this.sut.Compact();

            Assert.Single(this.sut.CoreBlock.Data);
            Assert.Null(sut.SparseBlocks);

            Assert.True(this.sut.ToList().SequenceEqual([500]));
        }

        [Fact]
        public void ClearingList()
        {
            this.sut.Add(100);
            this.sut.Add(101);

            this.sut.Clear();

            Assert.Equal(0, this.sut.Count);
        }

        [Fact]
        public void AddingToClearedList()
        {
            this.sut.Add(100);
            this.sut.Add(101);

            this.sut.Clear();

            this.sut.Add(102);

            Assert.Equal(1, this.sut.Count);
            Assert.True(this.sut.Contains(102));
            Assert.True(this.sut.ToList().SequenceEqual([102]));
        }
    }
}