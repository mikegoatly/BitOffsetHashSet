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
        public void AddingExistingEntry()
        {
            this.sut.Add(100);

            Assert.False(this.sut.Add(100));

            Assert.Single(this.sut.DataBlocks);
            Assert.Equal(1, this.sut.Count);
        }

        [Fact]
        public void AddingIntoEmptyList()
        {
            Assert.Empty(this.sut.DataBlocks);

            Assert.True(this.sut.Add(100));

            Assert.Single(this.sut.DataBlocks);
            Assert.Equal(1, this.sut.Count);

            Assert.True(this.sut.Contains(100));

            Assert.False(this.sut.Contains(99));
            Assert.False(this.sut.Contains(101));

            Assert.True(this.sut.ToList().SequenceEqual([100]));
        }

        [Fact]
        public void Adding1000()
        {
            for (int i = 0; i < 1000; i++)
            {
                // Add once - returns true
                Assert.True(this.sut.Add(i));

                // Adding again should return false
                Assert.False(this.sut.Add(i));
            }

            Assert.Equal(1000, this.sut.Count);

            Assert.True(this.sut.ToList().SequenceEqual(Enumerable.Range(0, 1000)));
        }

        [Fact]
        public void Adding1000Reversed()
        {
            for (int i = 999; i >= 0; i--)
            {
                // Add once - returns true
                Assert.True(this.sut.Add(i));

                // Adding again should return false
                Assert.False(this.sut.Add(i));
            }

            Assert.Equal(1000, this.sut.Count);

            Assert.True(this.sut.ToList().SequenceEqual(Enumerable.Range(0, 1000)));
        }

        [Fact]
        public void ConstructingFromExisting()
        {
            var original = new BitOffsetHashSet(1)
            {
                300,
                301,

                // Push the block size to 2
                1400
            };

            original.Remove(1400);

            this.sut = new BitOffsetHashSet(original);

            Assert.True(original.Contains(300));
            Assert.True(this.sut.Contains(301));

            Assert.Equal(256, this.sut.DataBlocks[0].BaseOffset);

            Assert.True(this.sut.ToList().SequenceEqual([300, 301]));

            // If we had done an enumeration copy, the buffer length would be 1 again
            Assert.Equal(2, this.sut.DataBlocks.Count);
        }

        [Fact]
        public void ConstructingFromList()
        {
            this.sut = new BitOffsetHashSet(
                [
                    300, 258, 3000
                ]);

            Assert.Equal(3, this.sut.Count);
            Assert.Equal(256, this.sut.DataBlocks[0].BaseOffset);
            Assert.True(this.sut.ToList().SequenceEqual([258, 300, 3000]));
        }

        [Fact]
        public void ConstructingFromHashSet()
        {
            var hashSet = new HashSet<int>() { 300, 258, 3000 };
            this.sut = new BitOffsetHashSet(hashSet);

            Assert.Equal(3, this.sut.Count);
            Assert.Equal(256, this.sut.DataBlocks[0].BaseOffset);
            Assert.True(this.sut.ToList().SequenceEqual([258, 300, 3000]));
        }

        [Fact]
        public void ConstructingFromListWithDuplicates()
        {
            this.sut = new BitOffsetHashSet(
                [
                    300, 258, 3000, 300, 258, 3000
                ]);

            Assert.Equal(3, this.sut.Count);
            Assert.Equal(256, this.sut.DataBlocks[0].BaseOffset);
            Assert.True(this.sut.ToList().SequenceEqual([258, 300, 3000]));
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
            Assert.Equal(0, this.sut.DataBlocks[0].BaseOffset);
            Assert.True(this.sut.ToList().SequenceEqual([100, 150, 300]));
        }

        [Fact]
        public void ConstructingFromEnumerableWithDuplicates()
        {
            static IEnumerable<int> Generator()
            {
                yield return 3000;
                yield return 300;
                yield return 1500;
                yield return 3000;
                yield return 300;
                yield return 1500;
            }

            this.sut = new BitOffsetHashSet(Generator());

            Assert.Equal(3, this.sut.Count);
            Assert.Equal(256, this.sut.DataBlocks[0].BaseOffset);
            Assert.True(this.sut.ToList().SequenceEqual([300, 1500, 3000]));
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

            Assert.Equal(2, this.sut.DataBlocks.Count);
            Assert.Equal(0, this.sut.DataBlocks[0].BaseOffset);
            Assert.Equal(24832, this.sut.DataBlocks[1].BaseOffset);
        }

        [Fact]
        public void Adding_IntoExistingSparseBlock()
        {
            this.sut.Add(100);

            Assert.True(this.sut.Add(25000));
            Assert.True(this.sut.Add(25002));

            Assert.Equal(3, this.sut.Count);

            Assert.True(this.sut.ToList().SequenceEqual([100, 25000, 25002]));

            Assert.Equal(24832, this.sut.DataBlocks[1].BaseOffset);
        }

        [Fact]
        public void Adding_BeforeExistingSparseBlock()
        {
            this.sut.Add(100);

            Assert.True(this.sut.Add(25000));
            Assert.True(this.sut.Add(22900));

            Assert.Equal(3, this.sut.Count);

            Assert.True(this.sut.ToList().SequenceEqual([100, 22900, 25000]));

            Assert.Equal(3, this.sut.DataBlocks.Count);

            Assert.Equal(0, this.sut.DataBlocks[0].BaseOffset);
            Assert.Equal(22784, this.sut.DataBlocks[1].BaseOffset);
            Assert.Equal(24832, this.sut.DataBlocks[2].BaseOffset);

        }

        [Fact]
        public void Adding_AfterExistingSparseBlock()
        {
            this.sut.Add(100);

            Assert.True(this.sut.Add(22900));
            Assert.True(this.sut.Add(25000));

            Assert.Equal(3, this.sut.Count);

            Assert.True(this.sut.ToList().SequenceEqual([100, 22900, 25000]));

            Assert.Equal(3, this.sut.DataBlocks.Count);

            Assert.Equal(0, this.sut.DataBlocks[0].BaseOffset);
            Assert.Equal(22784, this.sut.DataBlocks[1].BaseOffset);
            Assert.Equal(24832, this.sut.DataBlocks[2].BaseOffset);
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
            this.sut.Add(3500);

            Assert.Equal(2, this.sut.DataBlocks.Count);

            Assert.True(this.sut.Remove(3500));

            Assert.Equal(2, this.sut.DataBlocks.Count);

            Assert.Equal(1, this.sut.Count);

            Assert.True(this.sut.Contains(100));
            Assert.False(this.sut.Contains(3500));
            Assert.True(this.sut.ToList().SequenceEqual([100]));
        }

        [Fact]
        public void CompactingShouldRemoveEmptyBlocks()
        {
            this.sut.Add(100);
            this.sut.Add(3500);
            this.sut.Add(5000);

            Assert.Equal(3, this.sut.DataBlocks.Count);

            this.sut.Remove(100);
            this.sut.Remove(5000);

            Assert.Equal(3, this.sut.DataBlocks.Count);

            this.sut.Compact();

            Assert.Single(this.sut.DataBlocks);

            Assert.True(this.sut.ToList().SequenceEqual([3500]));
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