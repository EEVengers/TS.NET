using System.Buffers.Binary;
using System.Text;

namespace TS.NET;

public static class ThunderscopeNonVolatileMemory
{
    public static bool TryReadUserCalibration(Driver.Libtslitex.Thunderscope thunderscope, out Calibration? calibration)
    {
        calibration = null;
        // Read 4 + 4 bytes
        //    4 byte UTF8 sequence 'UCAL' (FCAL elsewhere, user/factory cal)
        //    i32 length of JSON calibration data
        Span<byte> preambleBytes = new byte[8];
        int preambleBytesRead = thunderscope.UserDataRead(preambleBytes, 0);
        var preambleSequence = Encoding.ASCII.GetString(preambleBytes.Slice(0, 4));
        if (preambleSequence != "UCAL")
            return false;
        var lengthBytes = preambleBytes.Slice(4, 4);
        var payloadLength = BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);
        if (payloadLength < 100 || payloadLength > 1048576)
            return false;

        Span<byte> payloadBytes = new byte[payloadLength];
        int payloadBytesRead = thunderscope.UserDataRead(payloadBytes, 8);
        if (payloadBytesRead != payloadLength)
            return false;

        var jsonString = Encoding.ASCII.GetString(payloadBytes);

        var crc32Bytes = new byte[4];
        thunderscope.UserDataRead(crc32Bytes, 8 + payloadLength);
        var crc32Calculated = Crc32(payloadBytes);
        var crcMatch = crc32Bytes.SequenceEqual(crc32Calculated);
        // To do: use crcMatch when production units released

        // To do: enable version check when an updated version occurs
        //var json = JsonDocument.Parse(jsonString);
        //var version = json.RootElement.GetProperty("version").GetInt32();
        //if (version != 1)
        //    throw new NotImplementedException();
        calibration = Calibration.FromDeviceJson(jsonString);
        return true;
    }

    public static void WriteUserCalibration(Driver.Libtslitex.Thunderscope thunderscope, Calibration calibration)
    {
        var json = calibration.ToDeviceJson();
        var jsonBytes = Encoding.ASCII.GetBytes(json);

        Span<byte> data = new byte[4 + 4 + jsonBytes.Length + 4 + 4096];      // Tag + length + JSON + CRC32 + padding
        data.Fill(0xFF);
        Encoding.ASCII.GetBytes("UCAL").CopyTo(data.Slice(0, 4));
        BinaryPrimitives.WriteUInt32BigEndian(data.Slice(4, 4), (uint)jsonBytes.Length);
        jsonBytes.CopyTo(data.Slice(8));
        var crc32 = Crc32(jsonBytes);
        crc32.CopyTo(data.Slice(8 + jsonBytes.Length, 4));
        thunderscope.UserDataWrite(data, 0);
    }

    public static Span<byte> BuildHwidTLV(Hwid hwid)
    {
        var json = hwid.ToDeviceJson();
        var jsonBytes = Encoding.ASCII.GetBytes(json);

        Span<byte> data = new byte[4 + 4 + jsonBytes.Length + 4];      // Tag + length + JSON + CRC32
        data.Fill(0xFF);
        Encoding.ASCII.GetBytes("HWID").CopyTo(data.Slice(0, 4));
        BinaryPrimitives.WriteUInt32BigEndian(data.Slice(4, 4), (uint)jsonBytes.Length);
        jsonBytes.CopyTo(data.Slice(8));
        var crc32 = Crc32(jsonBytes);
        crc32.CopyTo(data.Slice(8 + jsonBytes.Length, 4));
        return data;
    }

    public static byte[] Crc32(ReadOnlySpan<byte> bytes)
    {
        return System.IO.Hashing.Crc32.Hash(bytes).Reverse().ToArray();      // Network order/big endian
    }
}
