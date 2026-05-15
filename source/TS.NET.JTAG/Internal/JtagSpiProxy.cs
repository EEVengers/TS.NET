using System.Diagnostics;

namespace TS.NET.JTAG;

internal sealed class JtagSpiProxy
{
    private readonly D2xxJtagTapController tap;
    private readonly int chainLength;
    private readonly int chainIndex;

    public JtagSpiProxy(D2xxJtagTapController tap, int chainLength, int chainIndex)
    {
        this.tap = tap ?? throw new ArgumentNullException(nameof(tap));

        if (chainLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chainLength));
        }

        if (chainIndex < 0 || chainIndex >= chainLength)
        {
            throw new ArgumentOutOfRangeException(nameof(chainIndex));
        }

        this.chainLength = chainLength;
        this.chainIndex = chainIndex;
    }

    public void WriteEnable()
    {
        Write(SpiFlashOpcodes.WriteEnable, ReadOnlySpan<byte>.Empty);
    }

    public void WaitWhileBusy(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var status = Read(SpiFlashOpcodes.ReadStatusRegister, ReadOnlySpan<byte>.Empty, 1);
            if (status.Length > 0 && (status[0] & 0x01) == 0)
            {
                return;
            }

            if (cancellationToken.WaitHandle.WaitOne(5))
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        throw new TimeoutException("Timed out waiting for SPI flash busy flag to clear.");
    }

    public byte[] Read(byte command, ReadOnlySpan<byte> writeBytes, int readLength)
    {
        return Transfer(command, writeBytes, readLength);
    }

    public void Write(byte command, ReadOnlySpan<byte> writeBytes)
    {
        _ = Transfer(command, writeBytes, 0);
    }

    private byte[] Transfer(byte command, ReadOnlySpan<byte> writeBytes, int readLength)
    {
        if (readLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(readLength));
        }

        var bits = new List<bool>((1 + 32) + ((1 + writeBytes.Length + readLength) * 8) + chainLength + 8)
        {
            true, // JTAG2SPI marker
        };

        var xferBitsMinusOne = ((1 + writeBytes.Length + readLength) * 8) - 1;
        for (var i = 31; i >= 0; i--)
        {
            bits.Add(((xferBitsMinusOne >> i) & 1) != 0);
        }

        AppendByteMsbFirst(bits, command);
        for (var i = 0; i < writeBytes.Length; i++)
        {
            AppendByteMsbFirst(bits, writeBytes[i]);
        }

        var readOffset = -1;
        if (readLength > 0)
        {
            for (var i = 0; i < chainLength; i++)
            {
                bits.Add(false);
            }

            readOffset = bits.Count;
            for (var i = 0; i < readLength * 8; i++)
            {
                bits.Add(false);
            }
        }

        var payloadBytes = PackBitsToBytesLsb(bits);
        var payloadRead = tap.ShiftDrReadWriteTarget(chainLength, chainIndex, payloadBytes, bits.Count);

        if (readLength == 0)
        {
            return Array.Empty<byte>();
        }

        var rawRead = new byte[readLength];
        for (var bit = 0; bit < readLength * 8; bit++)
        {
            if (BitManipulation.GetBit(payloadRead, readOffset + bit))
            {
                rawRead[bit / 8] |= (byte)(1 << (bit % 8));
            }
        }

        BitManipulation.ReverseBitsInPlace(rawRead);
        return rawRead;
    }

    private static void AppendByteMsbFirst(List<bool> bits, byte value)
    {
        for (var i = 7; i >= 0; i--)
        {
            bits.Add(((value >> i) & 1) != 0);
        }
    }

    private static byte[] PackBitsToBytesLsb(List<bool> bits)
    {
        var bytes = new byte[(bits.Count + 7) / 8];
        for (var i = 0; i < bits.Count; i++)
        {
            if (bits[i])
            {
                BitManipulation.SetBit(bytes, i, true);
            }
        }

        return bytes;
    }

}
