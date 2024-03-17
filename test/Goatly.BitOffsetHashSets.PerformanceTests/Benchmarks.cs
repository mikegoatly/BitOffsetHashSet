using System;
using System.Collections.Generic;

using BenchmarkDotNet;
using BenchmarkDotNet.Attributes;

namespace Goatly.BitOffsetHashSets.PerformanceTests
{
    public class AddBenchmarks
    {
        private HashSet<int> hashSet;
        private BitOffsetHashSet bitOffsetHashSet;

        [IterationSetup]
        public void Setup()
        {
            this.hashSet = new HashSet<int>();
            this.bitOffsetHashSet = new BitOffsetHashSet(4);
        }

        [Benchmark(Baseline = true, OperationsPerInvoke = 10)]
        public void HashSetAdd()
        {
            for (int i = 0; i < 100; i++)
            {
                this.hashSet.Add(i);
            }
        }

        [Benchmark(OperationsPerInvoke = 100)]
        public void BitOffsetHashSetAdd()
        {
            for (int i = 0; i < 10; i++)
            {
                this.bitOffsetHashSet.Add(i);
            }
        }
    }

    public class ContainsBenchmarks
    {
        private HashSet<int> populatedHashSet;
        private BitOffsetHashSet populatedBitOffsetHashSet;

        [GlobalSetup]
        public void Setup()
        {
            this.populatedHashSet = new HashSet<int>();
            this.populatedBitOffsetHashSet = new BitOffsetHashSet(4);

            for (var i = 0; i < 100; i += 2)
            {
                this.populatedHashSet.Add(i);
                this.populatedBitOffsetHashSet.Add(i);
            }
        }

        [Benchmark(Baseline = true)]
        public bool HashSetGet()
        {
            var contains = false;
            for (int i = 0; i < 100; i++)
            {
                contains = this.populatedHashSet.Contains(i);
            }

            return contains;
        }

        [Benchmark()]
        public bool BitOffsetHashSetGet()
        {
            var contains = false;
            for (int i = 0; i < 100; i++)
            {
                contains = this.populatedBitOffsetHashSet.Contains(i);
            }

            return contains;
        }
    }
}
