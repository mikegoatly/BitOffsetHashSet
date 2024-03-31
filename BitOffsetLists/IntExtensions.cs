using System.Runtime.CompilerServices;

namespace Goatly.BitOffsetHashSets
{
    internal static class IntExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AlignTo64BitBoundary(this int value)
        {
            return value & ~63;
        }
    }
}
