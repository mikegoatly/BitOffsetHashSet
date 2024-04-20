using System.Runtime.CompilerServices;

namespace Goatly.BitOffsetHashSets
{
    internal static class IntExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AlignTo256BitBoundary(this int value)
        {
            return value & ~0xFF;
        }
    }
}
