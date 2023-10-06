using System.Text.Json;

namespace TS.NET
{
    // This has the potential to be a complex structure with per-PGA-gain-setting gain & offset, etc. Bodge for now.
    public class ThunderscopeCalibration
    {
        public ushort Channel1_Dac0V { get; set; }
        public ushort Channel2_Dac0V { get; set; }
        public ushort Channel3_Dac0V { get; set; }
        public ushort Channel4_Dac0V { get; set; }
    }

    public static class ThunderscopeCalibrationFile
    {
        public static ThunderscopeCalibration Read(string fileName)
        {
            var json = File.ReadAllText(fileName);
            return JsonSerializer.Deserialize<ThunderscopeCalibration>(json);
        }

        public static void Write(ThunderscopeCalibration calibration, string fileName)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(calibration, options);
            File.WriteAllText(fileName, json);
        }
    }
}
