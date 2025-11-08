using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class BufferInputVppStep : Step
{
    public BufferInputVppStep(string name, int channelIndex, int pathIndex, BenchCalibrationVariables variables) : base(name)
    {
        // A previous step should set Instruments.Instance.EnableSdgDc(channelIndex) to enable sig gen output
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.SetThunderscopeChannel([channelIndex]);
            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(1_000_000_000);

            Instruments.Instance.SetSdgDc(0);

            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, pathIndex, variables);
            var pathConfig = Utility.GetChannelPathConfig(channelIndex, pathIndex, variables);
            Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, variables);
            
            var zeroValue = Utility.GetAndCheckSigGenZero(channelIndex, pathConfig, variables, cancellationToken);
            var vpp = Utility.FindVpp(channelIndex, pathConfig, zeroValue, cancellationToken);

            pathCalibration.BufferInputVpp = Math.Round(vpp, 4);
            //pathCalibration.TrimOffsetDacScaleV = Math.Round(pathConfig.AdcCountPerDacCount * (pathCalibration.BufferInputVpp / 256.0), 6);
            variables.ParametersSet += 2;

            Result!.Summary = $"Vpp: {vpp:F4}";
            Logger.Instance.Log(LogLevel.Information, Index, Status.Passed, $"Vpp: {vpp:F4}");
            return Status.Passed;
        };
    }
}
