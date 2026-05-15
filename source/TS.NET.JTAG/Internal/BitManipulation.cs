namespace TS.NET.JTAG;

internal static class BitManipulation
{
    public static bool GetBit(byte[] data, int bitIndex)
    {
        return (data[bitIndex / 8] & (1 << (bitIndex % 8))) != 0;
    }

    public static void SetBit(byte[] data, int bitIndex, bool value)
    {
        if (value)
        {
            data[bitIndex / 8] |= (byte)(1 << (bitIndex % 8));
        }
        else
        {
            data[bitIndex / 8] &= (byte)~(1 << (bitIndex % 8));
        }
    }

    public static byte ReverseBits(byte value)
    {
        value = (byte)((value >> 4) | (value << 4));
        value = (byte)(((value & 0xCC) >> 2) | ((value & 0x33) << 2));
        value = (byte)(((value & 0xAA) >> 1) | ((value & 0x55) << 1));
        return value;
    }

    public static void ReverseBitsInPlace(byte[] bytes)
    {
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = ReverseBits(bytes[i]);
        }
    }
}