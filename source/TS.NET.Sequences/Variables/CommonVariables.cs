namespace TS.NET.Sequences;

public class CommonVariables
{
    public int FrontEndSettlingTimeMs { get; set; } = 300;
    public int TrimSettlingTimeMs { get; set; } = 30;
    public Calibration Calibration { get; set; } = new();
    public string HwidSerial { get; set; } = "";
}
