using System.Text.Json;

namespace TS.NET.Engine
{
    public class ThunderscopeSettings
    {
        public string Driver { get; set; }
        public ulong MaxChannelBytes { get; set; }
        public ThunderscopeCalibration Calibration { get; set; }

        public static ThunderscopeSettings Default()
        {
            return new ThunderscopeSettings()
            {
                Driver = "XDMA",
                MaxChannelBytes = 1000000,
                Calibration = ThunderscopeCalibration.Default()
            };
        }

        public static ThunderscopeSettings FromFile(string file)
        {
            if (!File.Exists(file))
                throw new FileNotFoundException(file);

            return JsonSerializer.Deserialize<ThunderscopeSettings>(File.ReadAllText(file));
        }
    }
}
