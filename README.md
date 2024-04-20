# BitOffsetHashSet

## What is it?

A specialized hash set for integers. It shines when you know that values are going to be relatively closely grouped together,
e.g. when you have a small number of integers that you are tracking.

## How does it work?

Instead of storing each integer in a separate bucket, integers are stored as a bit in a bitset. This means that when
values are closely grouped together, the memory usage is much lower than a traditional hash set.

Currently a bucket is structured like this:

```cs
int BaseOffset // The base offset for the bucket, aligned to a 256bit boundary
int MaxValue // The maximum value in the bucket
ulong Data1..Data4 // The bitset data
```

Each bucket can store 256 bits, which means that it can store 256 integers as long as they are consecutive. 

The hash set can make use of multiple buckets, so if sparse data is encountered, it can still cater for it.

## How do I use it?

```cs
var set = new BitOffsetHashSet();
set.Add(1); // true
set.Add(2); // true

// Duplicate additions are ignored
set.Add(1); // false

set.Contains(1); // true
set.Contains(3); // false

set.Remove(1); // true
set.Contains(1); // false
set.Remove(1); // false
```

## How does it perform?

In benchmark tests, the BitOffsetHashSet is faster than a traditional HashSet when the number of integers is small and
closely grouped together.

### Adding sequential values

| Method | Count | Mean | Error | StdDev | Median | Allocated |
|-|-|-|-|-|-|-|
|BitOffsetHashSetBenchmarks|1|333.333 ns|46.5456 ns|134.2948 ns|300.000 ns|400 B|
|HashSetBenchmarks|1|1,246.316 ns|69.2991 ns|198.8320 ns|1,200.000 ns|504 B|
|BitOffsetHashSetBenchmarks|10|681.720 ns|48.7412 ns|138.2706 ns|700.000 ns|400 B|
|HashSetBenchmarks|10|2,011.957 ns|115.9737 ns|327.1060 ns|2,000.000 ns|1000 B|
|BitOffsetHashSetBenchmarks|100|3,938.542 ns|347.7356 ns|1,003.2966 ns|3,600.000 ns|400 B|
|HashSetBenchmarks|100|3,753.226 ns|165.9474 ns|470.7652 ns|3,750.000 ns|6336 B|
|BitOffsetHashSetBenchmarks|1000|40,858.763 ns|2,020.5265 ns|5,861.9137 ns|39,400.000 ns|400 B|
|HashSetBenchmarks|1000|21,849.462 ns|747.4094 ns|2,120.2760 ns|21,300.000 ns|59000 B|
|BitOffsetHashSetBenchmarks|10000|407,091.176 ns|7,932.9624 ns|12,810.2555 ns|405,400.000 ns|5296 B|
|HashSetBenchmarks|10000|118,553.333 ns|1,719.9525 ns|1,608.8446 ns|118,600.000 ns|538960 B|

As can be seen, the BitOffsetHashSet is at least as fast as HashSet when adding around 100 close values,
but memory usage is *always* lower. Performance drops as the number of values increases, but at 10,000 values
the memory usage is 2 orders of magnitude lower than HashSet.