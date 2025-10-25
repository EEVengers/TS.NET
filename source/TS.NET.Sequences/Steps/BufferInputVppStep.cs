using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class BufferInputVppStep : Step
{
    public BufferInputVppStep(string name, int channelIndex, int pathIndex, BenchCalibrationVariables variables) : base(name)
    {
        // A previous step should set Instruments.Instance.EnableSdgDc(channelIndex) to enable sig gen output
        Action = (CancellationToken cancellationToken) =>
        {
            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, pathIndex, variables);
            var pathConfig = Utility.GetChannelPathConfig(channelIndex, pathIndex, variables);
            Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, variables);
            
            var zeroValue = Utility.GetAndCheckSigGenZero(channelIndex, pathConfig, variables, cancellationToken);
            var vpp = Utility.FindVpp(channelIndex, pathConfig, zeroValue, cancellationToken);

            pathCalibration.BufferInputVpp = Math.Round(vpp, 4);
            //pathCalibration.TrimOffsetDacScaleV = Math.Round(pathConfig.AdcCountPerDacCount * (pathCalibration.BufferInputVpp / 255.0), 6);
            variables.ParametersSet += 2;

            Logger.Instance.Log(LogLevel.Information, Index, Status.Passed, $"Vpp: {vpp:F4}");
            return Status.Passed;
        };
    }
}
