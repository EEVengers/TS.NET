namespace TS.NET.JTAG;

internal sealed class D2xxJtagTapController
{
    [Flags]
    private enum MpsseShiftOpcode : byte
    {
        WriteNegativeEdge = 0x01,
        BitMode = 0x02,
        LsbFirst = 0x08,
        Write = 0x10,
        Read = 0x20,
    }

    private const byte MpsseClockTmsOut = 0x4B;
    private const byte MpsseClockTmsInOut = 0x6B;
    private const byte MpsseSendImmediate = 0x87;

    private readonly D2xxMpsseDevice mpsse;

    public D2xxJtagTapController(D2xxMpsseDevice mpsse)
    {
        this.mpsse = mpsse;
    }

    public void ResetTap()
    {
        var cmd = new List<byte>(3);
        // Keep TMS high for 6 clocks to force Test-Logic-Reset, then one low clock to enter Run-Test/Idle.
        AddTms(cmd, 0x3F, 7, false, false);
        mpsse.Execute(cmd.ToArray(), 0);
    }

    public void RunIdleCycles(int cycles)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(cycles);

        while (cycles > 0)
        {
            var chunk = Math.Min(7, cycles);
            var cmd = new List<byte>(3);
            AddTms(cmd, 0, chunk, false, false);
            mpsse.Execute(cmd.ToArray(), 0);
            cycles -= chunk;
        }
    }

    public void ShiftIr(int bitLength, ushort instruction)
    {
        var bytes = new byte[(bitLength + 7) / 8];
        for (var i = 0; i < bitLength; i++)
        {
            if (((instruction >> i) & 1) != 0)
            {
                bytes[i / 8] |= (byte)(1 << (i % 8));
            }
        }

        ShiftRegisterWrite(isInstruction: true, bytes, bitLength);
    }

    public void ShiftIrWrite(byte[] writeData, int bitLength)
    {
        ShiftRegisterWrite(isInstruction: true, writeData, bitLength);
    }

    public void ShiftIrWriteTarget(int chainLength, int chainIndex, int irLength, ushort instruction, ushort bypassInstruction)
    {
        if (chainLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chainLength));
        }

        if (chainIndex < 0 || chainIndex >= chainLength)
        {
            throw new ArgumentOutOfRangeException(nameof(chainIndex));
        }

        if (irLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(irLength));
        }

        var frame = BuildInstructionFrame(chainLength, chainIndex, irLength, instruction, bypassInstruction);
        ShiftIrWrite(frame, chainLength * irLength);
    }

    public byte[] ShiftDrRead(int bitLength, byte fillByte)
    {
        var writeData = new byte[(bitLength + 7) / 8];
        Array.Fill(writeData, fillByte);

        return ShiftRegisterReadWrite(isInstruction: false, writeData, bitLength, readData: true);
    }

    public List<uint> ReadChainIdCodes(int irLength, ushort idCodeInstruction, int maxScanDevices)
    {
        if (irLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(irLength));
        }

        if (maxScanDevices <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxScanDevices));
        }

        ResetTap();
        ShiftIr(irLength, idCodeInstruction);

        var raw = ShiftDrRead(maxScanDevices * 32, 0xFF);
        var idCodes = new List<uint>(maxScanDevices);

        for (var i = 0; i < maxScanDevices; i++)
        {
            var offset = i * 4;
            var idCode =
                (uint)raw[offset] |
                ((uint)raw[offset + 1] << 8) |
                ((uint)raw[offset + 2] << 16) |
                ((uint)raw[offset + 3] << 24);

            if (idCode == 0xFFFF_FFFF)
            {
                break;
            }

            if ((idCode & 1) == 0)
            {
                break;
            }

            idCodes.Add(idCode);
        }

        return idCodes;
    }

    public void ShiftDrWrite(byte[] writeData, int bitLength, Action<int>? progressPercent = null)
    {
        if (bitLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitLength));
        }

        if (writeData.Length * 8 < bitLength)
        {
            throw new ArgumentException("Bit length exceeds provided data.", nameof(bitLength));
        }

        MoveToShiftDr();
        progressPercent?.Invoke(0);

        var bodyBits = bitLength - 1;
        var bitCursor = 0;

        while (bodyBits - bitCursor >= 8)
        {
            var chunkBits = Math.Min(bodyBits - bitCursor, 65_536 * 8);
            var chunkBytes = chunkBits / 8;

            var cmd = new List<byte>(3 + chunkBytes)
            {
                BuildShiftOpcode(readData: false, bitMode: false),
            };

            var lenMinusOne = (ushort)(chunkBytes - 1);
            cmd.Add((byte)(lenMinusOne & 0xFF));
            cmd.Add((byte)(lenMinusOne >> 8 & 0xFF));

            var byteOffset = bitCursor / 8;
            for (var i = 0; i < chunkBytes; i++)
            {
                cmd.Add(writeData[byteOffset + i]);
            }

            mpsse.Execute(cmd.ToArray(), 0);
            bitCursor += chunkBytes * 8;
            ReportBodyShiftProgress(progressPercent, bitCursor, bodyBits);
        }

        var remBits = bodyBits - bitCursor;
        if (remBits > 0)
        {
            var cmd = new List<byte>(3)
            {
                BuildShiftOpcode(readData: false, bitMode: true),
                (byte)(remBits - 1),
                ExtractBits(writeData, bitCursor, remBits),
            };

            mpsse.Execute(cmd.ToArray(), 0);
            bitCursor += remBits;
            ReportBodyShiftProgress(progressPercent, bitCursor, bodyBits);
        }

        var lastTdi = GetBit(writeData, bitLength - 1);
        var tail = new List<byte>(3);
        AddTms(tail, 0b011, 3, lastTdi, false);
        mpsse.Execute(tail.ToArray(), 0);
        progressPercent?.Invoke(100);
    }

    public byte[] ShiftDrReadWrite(byte[] writeData, int bitLength)
    {
        return ShiftRegisterReadWrite(isInstruction: false, writeData, bitLength, readData: true);
    }

    public byte[] ShiftDrReadWriteTarget(int chainLength, int chainIndex, byte[] payloadData, int payloadBitLength)
    {
        var prefixBits = chainIndex;
        var suffixBits = chainLength - chainIndex - 1;

        if (prefixBits == 0 && suffixBits == 0)
        {
            return ShiftDrReadWrite(payloadData, payloadBitLength);
        }

        var frame = InsertPayloadWithBypassBits(payloadData, payloadBitLength, prefixBits, suffixBits);
        var totalBits = prefixBits + payloadBitLength + suffixBits;
        var readFrame = ShiftDrReadWrite(frame, totalBits);
        return ExtractBitRange(readFrame, prefixBits, payloadBitLength);
    }

    public void ShiftDrWriteTarget(int chainLength, int chainIndex, byte[] payloadData, int payloadBitLength, Action<int>? progressPercent = null)
    {
        var prefixBits = chainIndex;
        var suffixBits = chainLength - chainIndex - 1;
        var totalBits = prefixBits + payloadBitLength + suffixBits;

        if (prefixBits == 0 && suffixBits == 0)
        {
            ShiftDrWrite(payloadData, payloadBitLength, progressPercent);
            return;
        }

        var frame = InsertPayloadWithBypassBits(payloadData, payloadBitLength, prefixBits, suffixBits);
        ShiftDrWrite(frame, totalBits, progress =>
        {
            if (progressPercent is null)
            {
                return;
            }

            if (payloadBitLength <= 0)
            {
                progressPercent(100);
                return;
            }

            var shiftedTotalBits = (long)progress * totalBits / 100;
            var shiftedPayloadBits = Math.Clamp(shiftedTotalBits - prefixBits, 0, payloadBitLength);
            var mapped = (int)(shiftedPayloadBits * 100 / payloadBitLength);
            progressPercent(mapped);
        });
    }

    private static void ReportBodyShiftProgress(Action<int>? progressPercent, int shiftedBodyBits, int totalBodyBits)
    {
        if (progressPercent is null)
        {
            return;
        }

        if (totalBodyBits <= 0)
        {
            progressPercent(99);
            return;
        }

        var percent = (int)Math.Clamp((long)shiftedBodyBits * 99 / totalBodyBits, 0, 99);
        progressPercent(percent);
    }

    private void ShiftRegisterWrite(bool isInstruction, byte[] writeData, int bitLength)
    {
        ShiftRegisterReadWrite(isInstruction, writeData, bitLength, readData: false);
    }

    private byte[] ShiftRegisterReadWrite(bool isInstruction, byte[] writeData, int bitLength, bool readData)
    {
        if (bitLength <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bitLength));
        }

        if (writeData.Length * 8 < bitLength)
        {
            throw new ArgumentException("Bit length exceeds provided data.", nameof(writeData));
        }

        var command = new List<byte>(128);
        var expectedReadBytes = 0;

        if (isInstruction)
        {
            AddTms(command, 0b0011, 4, false, false);
        }
        else
        {
            AddTms(command, 0b001, 3, false, false);
        }

        var bodyBits = bitLength - 1;
        var fullBytes = bodyBits / 8;
        var remBits = bodyBits % 8;

        if (fullBytes > 0)
        {
            var op = BuildShiftOpcode(readData, bitMode: false);
            command.Add(op);

            var lenMinusOne = (ushort)(fullBytes - 1);
            command.Add((byte)(lenMinusOne & 0xFF));
            command.Add((byte)(lenMinusOne >> 8 & 0xFF));

            for (var i = 0; i < fullBytes; i++)
            {
                command.Add(writeData[i]);
            }

            if (readData)
            {
                expectedReadBytes += fullBytes;
            }
        }

        if (remBits > 0)
        {
            var op = BuildShiftOpcode(readData, bitMode: true);
            command.Add(op);
            command.Add((byte)(remBits - 1));
            command.Add(ExtractBits(writeData, fullBytes * 8, remBits));

            if (readData)
            {
                expectedReadBytes += 1;
            }
        }

        var lastTdi = GetBit(writeData, bitLength - 1);
        AddTms(command, 0b011, 3, lastTdi, readData);
        if (readData)
        {
            expectedReadBytes += 1;
        }

        if (readData)
        {
            command.Add(MpsseSendImmediate); // send immediate
        }

        var read = mpsse.Execute(command.ToArray(), expectedReadBytes);
        if (!readData)
        {
            return Array.Empty<byte>();
        }

        var result = new byte[(bitLength + 7) / 8];
        var readIndex = 0;

        if (fullBytes > 0)
        {
            Buffer.BlockCopy(read, 0, result, 0, fullBytes);
            readIndex += fullBytes;
        }

        if (remBits > 0)
        {
            // FTCJTAG-style merge: partial-bit read byte plus final data bit captured during TMS transition.
            var remainingShift = 8 - remBits;
            result[fullBytes] = (byte)(read[readIndex] >> remainingShift);
            var tmsReadByte = read[readIndex + 1];
            var lastDataBit = (byte)(tmsReadByte << 2 & 0x80);
            result[fullBytes] |= (byte)(lastDataBit >> remainingShift - 1);
        }
        else
        {
            var tmsReadByte = read[readIndex];
            var lastDataBit = (byte)(tmsReadByte << 2 >> 7);
            result[fullBytes] = lastDataBit;
        }

        return result;
    }

    private void MoveToShiftDr()
    {
        var cmd = new List<byte>(3);
        AddTms(cmd, 0b001, 3, false, false);
        mpsse.Execute(cmd.ToArray(), 0);
    }

    private static byte BuildShiftOpcode(bool readData, bool bitMode)
    {
        var op = MpsseShiftOpcode.Write | MpsseShiftOpcode.LsbFirst | MpsseShiftOpcode.WriteNegativeEdge;
        if (readData)
        {
            op |= MpsseShiftOpcode.Read;
        }

        if (bitMode)
        {
            op |= MpsseShiftOpcode.BitMode;
        }

        return (byte)op;
    }

    private static byte[] BuildInstructionFrame(int chainLength, int chainIndex, int irLength, ushort instruction, ushort bypassInstruction)
    {
        var totalBits = chainLength * irLength;
        var frame = new byte[(totalBits + 7) / 8];

        for (var device = 0; device < chainLength; device++)
        {
            var value = device == chainIndex ? instruction : bypassInstruction;
            var startBit = device * irLength;
            for (var bit = 0; bit < irLength; bit++)
            {
                if (((value >> bit) & 1) != 0)
                {
                    BitManipulation.SetBit(frame, startBit + bit, true);
                }
            }
        }

        return frame;
    }

    private static byte[] InsertPayloadWithBypassBits(byte[] payloadData, int payloadBits, int prefixBits, int suffixBits)
    {
        var totalBits = prefixBits + payloadBits + suffixBits;
        var frame = new byte[(totalBits + 7) / 8];

        for (var i = 0; i < payloadBits; i++)
        {
            if (BitManipulation.GetBit(payloadData, i))
            {
                BitManipulation.SetBit(frame, prefixBits + i, true);
            }
        }

        return frame;
    }

    private static byte[] ExtractBitRange(byte[] source, int startBit, int bitLength)
    {
        var result = new byte[(bitLength + 7) / 8];
        for (var i = 0; i < bitLength; i++)
        {
            if (BitManipulation.GetBit(source, startBit + i))
            {
                BitManipulation.SetBit(result, i, true);
            }
        }

        return result;
    }

    private static void AddTms(List<byte> command, byte tmsBitsLsbFirst, int bitCount, bool tdiHigh, bool readTdo)
    {
        if (bitCount < 1 || bitCount > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(bitCount), "TMS chunks must be in the range 1..7.");
        }

        command.Add(readTdo ? MpsseClockTmsInOut : MpsseClockTmsOut);
        command.Add((byte)(bitCount - 1));

        var payloadMask = (byte)((1 << bitCount) - 1);
        var payload = (byte)(tmsBitsLsbFirst & payloadMask);
        if (tdiHigh)
        {
            payload |= 0x80;
        }

        command.Add(payload);
    }

    private static bool GetBit(byte[] data, int bitIndex)
    {
        return (data[bitIndex / 8] & (1 << (bitIndex % 8))) != 0;
    }

    private static byte ExtractBits(byte[] data, int startBit, int bitCount)
    {
        byte value = 0;
        for (var i = 0; i < bitCount; i++)
        {
            if ((data[(startBit + i) / 8] & (1 << ((startBit + i) % 8))) != 0)
            {
                value |= (byte)(1 << i);
            }
        }

        return value;
    }
}