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
        var payloadLength = BitConverter.ToInt32(preambleBytes.Slice(4, 4));
        if (payloadLength < 100 || payloadLength > 1048576)
            return false;

        Span<byte> payloadBytes = new byte[payloadLength];
        int payloadBytesRead = thunderscope.UserDataRead(payloadBytes, 8);
        if (payloadBytesRead != payloadLength)
            return false;

        var utf8 = Encoding.UTF8.GetString(payloadBytes);
        var json = JsonDocument.Parse(utf8);
        var schemaVersion = json.RootElement.GetProperty("SchemaVersion").GetString();
        if (schemaVersion != "0")
            throw new NotImplementedException();
        calibration = ThunderscopeCalibrationSettings.FromJson(utf8);
        return true;
    }

    public static void WriteUserCalibration(Driver.Libtslitex.Thunderscope thunderscope, ThunderscopeCalibrationSettings calibration)
    {
        var json = calibration.ToJson();
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        Span<byte> data = new byte[8 + jsonBytes.Length];
        Encoding.UTF8.GetBytes("UCAL").CopyTo(data.Slice(0, 4));
        BitConverter.GetBytes(jsonBytes.Length).CopyTo(data.Slice(4, 4));
        jsonBytes.CopyTo(data.Slice(8));

        thunderscope.UserDataWrite(data, 0);
    }
}
