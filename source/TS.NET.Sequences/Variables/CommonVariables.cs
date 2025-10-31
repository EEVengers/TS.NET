namespace TS.NET.Sequences;

public class CommonVariables
{
    public int WarmupTimeSec { get; set; } = 20 * 60;
    public int FrontEndSettlingTimeMs { get; set; } = 100;
    public ThunderscopeCalibrationSettings Calibration { get; set; } = new();
}
