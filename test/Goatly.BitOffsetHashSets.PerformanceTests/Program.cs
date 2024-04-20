using BenchmarkDotNet.Running;

namespace Goatly.BitOffsetHashSets.PerformanceTests
{
    public class Program
    {
        public static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
                .RunAllJoined(BenchmarkConfig.Get());
        }
    }
}