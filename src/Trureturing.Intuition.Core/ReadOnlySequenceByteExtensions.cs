using System.Buffers;

namespace Trureturing.Intuition.Core;

internal static class ReadOnlySequenceByteExtensions
{
    public static byte[] ToArray(this ReadOnlySequence<byte> value)
    {
        if (value.IsSingleSegment) return value.FirstSpan.ToArray();
        byte[] result = new byte[checked((int)value.Length)];
        value.CopyTo(result);
        return result;
    }
}
