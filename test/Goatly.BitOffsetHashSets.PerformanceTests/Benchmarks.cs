using System;
using System.Collections.Generic;

using BenchmarkDotNet;
using BenchmarkDotNet.Attributes;

namespace Goatly.BitOffsetHashSets.PerformanceTests
{
    public class Adding
    {
        public class HashSetBenchmarks
        {
            private HashSet<int> hashSet;
            private Random random;

            [IterationSetup]
            public void Setup()
            {
                this.hashSet = new HashSet<int>();
                this.random = new Random(0);
            }

            [Params(1, 10, 100, 1000, 10000)]
            public int Count { get; set; }

            [Benchmark()]
            public object AddSequential()
            {
                for (int i = 0; i < Count; i++)
                {
                    this.hashSet.Add(i);
                }

                return this.hashSet;
            }

            [Benchmark()]
            public object AddReverse()
            {
                for (int i = Count - 1; i >= 0; i--)
                {
                    this.hashSet.Add(i);
                }

                return this.hashSet;
            }

            [Benchmark()]
            public object AddRandom()
            {
                for (int i = 0; i < Count; i++)
                {
                    this.hashSet.Add(random.Next(Count));
                }

                return this.hashSet;
            }
        }

        public class BitOffsetHashSetBenchmarks
        {
            private BitOffsetHashSet bitOffsetHashSet;
            private Random random;

            [IterationSetup]
            public void Setup()
            {
                this.bitOffsetHashSet = new BitOffsetHashSet(4);
                this.random = new Random(0);
            }

            [Params(1, 10, 100, 1000, 10000)]
            public int Count { get; set; }

            [Benchmark()]
            public object AddSequential()
            {
                for (int i = 0; i < Count; i++)
                {
                    this.bitOffsetHashSet.Add(i);
                }

                return this.bitOffsetHashSet;
            }

            [Benchmark()]
            public object AddReverse()
            {
                for (int i = Count - 1; i >= 0; i--)
                {
                    this.bitOffsetHashSet.Add(i);
                }

                return this.bitOffsetHashSet;
            }

            [Benchmark()]
            public object AddRandom()
            {
                for (int i = 0; i < Count; i++)
                {
                    this.bitOffsetHashSet.Add(random.Next(Count));
                }

                return this.bitOffsetHashSet;
            }
        }
    }

    public class Contains
    {
        public class HashSetBenchmarks
        {
            private HashSet<int> populatedHashSet;

            [GlobalSetup]
            public void Setup()
            {
                this.populatedHashSet = new HashSet<int>();

                for (var i = 0; i < Count; i += 2)
                {
                    this.populatedHashSet.Add(i);
                }
            }

            [Params(1, 10, 100, 1000, 10000)]
            public int Count { get; set; }

            [Benchmark()]
            public bool Contains()
            {
                var contains = false;
                for (int i = 0; i < Count; i++)
                {
                    contains = this.populatedHashSet.Contains(i);
                }

                return contains;
            }
        }

        public class BitOffsetHashSetBenchmarks
        {
            private BitOffsetHashSet populatedBitOffsetHashSet;

            [GlobalSetup]
            public void Setup()
            {
                this.populatedBitOffsetHashSet = new BitOffsetHashSet(4);

                for (var i = 0; i < Count; i += 2)
                {
                    this.populatedBitOffsetHashSet.Add(i);
                }
            }

            [Params(1, 10, 100, 1000, 10000)]
            public int Count { get; set; }

            [Benchmark()]
            public bool Contains()
            {
                var contains = false;
                for (int i = 0; i < Count; i++)
                {
                    contains = this.populatedBitOffsetHashSet.Contains(i);
                }

                return contains;
            }
        }
    }
}
