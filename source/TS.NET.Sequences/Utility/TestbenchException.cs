using TS.NET.Sequences;

namespace TS.NET.Sequences;

public class TestbenchException : Exception
{
    public TestbenchException() { }
    public TestbenchException(string message) : base(message) { }

    public static void TestbenchExceptionIfTrimDacZeroNotCalibrated(CommonVariables variables)
    {
        if (!variables.TrimDacZeroCalibrated)
        {
            throw new TestbenchException($"Trim DAC zero must be calibrated before running {nameof(BodePlotStep)}");
        }
    }

    public static void TestbenchExceptionIfTrimDacScaleNotCalibrated(CommonVariables variables)
    {
        if (!variables.TrimDacScaleCalibrated)
        {
            throw new TestbenchException($"Trim DAC scale must be calibrated before running {nameof(BodePlotStep)}");
        }
    }

    public static void TestbenchExceptionIfBufferInputVppNotCalibrated(CommonVariables variables)
    {
        if (!variables.BufferInputVppCalibrated)
        {
            throw new TestbenchException($"Buffer input Vpp must be calibrated before running {nameof(BodePlotStep)}");
        }
    }

    public static void TestbenchExceptionIfAdcBranchGainsNotCalibrated(CommonVariables variables)
    {
        if (!variables.AdcBranchGainsCalibrated)
        {
            throw new TestbenchException($"ADC branch gains must be calibrated before running {nameof(BodePlotStep)}");
        }
    }
}
