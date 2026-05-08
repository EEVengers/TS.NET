using System.Text;
using System.Text.Json;

namespace TS.NET;

public static class ThunderscopeNonVolatileMemory
{
    public static bool TryReadUserCalibration(Driver.Libtslitex.Thunderscope thunderscope, out ThunderscopeCalibrationSettings? calibration)
    {
        calibration = null;
        // Read 4 + 4 bytes
        //    4 byte UTF8 sequence 'UCAL' (FCAL elsewhere, user/factory cal)
        //    i32 length of JSON calibration data
        Span<byte> preambleBytes = new byte[8];
        int preambleBytesRead = thunderscope.UserDataRead(preambleBytes, 0);
        var preambleSequence = Encoding.UTF8.GetString(preambleBytes.Slice(0, 4));
        if (preambleSequence != "UCAL")
            return false;
        var lengthBytes = preambleBytes.Slice(4, 4);
        if (BitConverter.IsLittleEndian)
            lengthBytes.Reverse();
        var payloadLength = BitConverter.ToInt32(lengthBytes);
        if (payloadLength < 100 || payloadLength > 1048576)
            return false;

        Span<byte> payloadBytes = new byte[payloadLength];
        int payloadBytesRead = thunderscope.UserDataRead(payloadBytes, 8);
        if (payloadBytesRead != payloadLength)
            return false;

        var utf8 = Encoding.UTF8.GetString(payloadBytes);

        var crc32Bytes = new byte[4];
        thunderscope.UserDataRead(crc32Bytes, 8 + payloadLength);
        var crc32Calculated = Crc32(payloadBytes);
        var crcMatch = crc32Bytes.SequenceEqual(crc32Calculated);
        // To do: use crcMatch when production units released

        var json = JsonDocument.Parse(utf8);
        // To do: enable version check when an updated version occurs
        //var version = json.RootElement.GetProperty("version").GetInt32();
        //if (version != 1)
        //    throw new NotImplementedException();
        calibration = ThunderscopeCalibrationSettings.FromDeviceJson(utf8);
        return true;
    }

    public static void WriteUserCalibration(Driver.Libtslitex.Thunderscope thunderscope, ThunderscopeCalibrationSettings calibration)
    {
        var json = calibration.ToDeviceJson();
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        Span<byte> data = new byte[8 + jsonBytes.Length + 4 + 4096];      // Tag + length + JSON + CRC32 + padding
        data.Fill(0xFF);
        Encoding.UTF8.GetBytes("UCAL").CopyTo(data.Slice(0, 4));
        var lengthBytes = BitConverter.GetBytes(jsonBytes.Length);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(lengthBytes);
        lengthBytes.CopyTo(data.Slice(4, 4));
        jsonBytes.CopyTo(data.Slice(8));
        var crc32 = Crc32(jsonBytes);
        crc32.CopyTo(data.Slice(8 + jsonBytes.Length, 4));
        thunderscope.UserDataWrite(data, 0);
    }

    public static byte[] Crc32(ReadOnlySpan<byte> bytes)
    {
        return System.IO.Hashing.Crc32.Hash(bytes).Reverse().ToArray();      // Network order/big endian
    }
}
