namespace TS.NET.Sequences;

public class CommonVariables
{
    public int FrontEndSettlingTimeMs { get; set; } = 300;
    public int TrimSettlingTimeMs { get; set; } = 30;
    public string HwidSerial { get; set; } = "";

    public Calibration Calibration { get; set; } = new();
    public bool TrimDacZeroCalibrated { get; set; } = false;
    public bool TrimDacScaleCalibrated { get; set; } = false;
    public bool BufferInputVppCalibrated { get; set; } = false;
    public bool LoadScalesCalibrated { get; set; } = false;
    public bool AdcBranchGainsCalibrated { get; set; } = false;
}
