namespace Goatly.BitOffsetHashSets.Tests.Unit
{
    public class BitOffsetHashSetTests
    {
        private BitOffsetHashSet sut;

        public BitOffsetHashSetTests()
        {
            this.sut = [];
        }

        [Fact]
        public void AddingIntoEmptyList()
        {
            Assert.Equal(1, this.sut.BitDataBufferLength);

            Assert.True(this.sut.Add(100));

            Assert.Equal(1, this.sut.BitDataBufferLength);
            Assert.Equal(1, this.sut.Count);

            Assert.True(this.sut.Contains(100));

            Assert.False(this.sut.Contains(99));
            Assert.False(this.sut.Contains(101));

            Assert.True(this.sut.ToList().SequenceEqual([100]));
        }

        [Fact]
        public void ConstructingFromExisting()
        {
            var original = new BitOffsetHashSet();

            original.Add(100);
            original.Add(101);

            // Push the block size to 2
            original.Add(140);
            original.Remove(140);

            this.sut = new BitOffsetHashSet(original);

            Assert.True(original.Contains(100));
            Assert.True(this.sut.Contains(101));

            Assert.Equal(64, this.sut.BaseOffset);

            Assert.True(this.sut.ToList().SequenceEqual([100, 101]));

            // If we had done an enumeration copy, the buffer length would be 1 again
            Assert.Equal(2, this.sut.BitDataBufferLength);
        }

        [Fact]
        public void ConstructingFromList()
        {
            this.sut = new BitOffsetHashSet(
                [
                    100, 150, 300
                ]);

            Assert.Equal(3, this.sut.Count);
            Assert.Equal(64, this.sut.BaseOffset);
            Assert.True(this.sut.ToList().SequenceEqual([100, 150, 300]));
        }

        [Fact]
        public void ConstructingFromHashSet()
        {
            this.sut = new BitOffsetHashSet(
                new HashSet<int>() { 100, 150, 300 });

            Assert.Equal(3, this.sut.Count);
            Assert.Equal(64, this.sut.BaseOffset);
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
            Assert.Equal(64, this.sut.BaseOffset);
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
            Assert.Equal(64, this.sut.BaseOffset);
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
            Assert.Equal(64, this.sut.BaseOffset);
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

            Assert.Equal(5, this.sut.BitDataBufferLength);

            Assert.True(this.sut.Remove(350));

            Assert.Equal(5, this.sut.BitDataBufferLength);

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

            Assert.Equal(10, this.sut.BitDataBufferLength);

            this.sut.Remove(100);
            this.sut.Remove(500);

            Assert.Equal(10, this.sut.BitDataBufferLength);

            this.sut.Compact();

            Assert.Equal(1, this.sut.BitDataBufferLength);

            Assert.True(this.sut.ToList().SequenceEqual([350]));
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