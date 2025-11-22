using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class FindSigGenZeroStep : Step
{
    public FindSigGenZeroStep(string name, int channelIndex, BenchCalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            var pathConfig = Utility.GetChannelPathConfig(0, 0, variables);
            var pathCalibration = Utility.GetChannelPathCalibration(0, 0, variables);
            Instruments.Instance.SetThunderscopeCalManual1M(0, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, variables.FrontEndSettlingTimeMs);
            SigGens.Instance.SetSdgChannel([channelIndex]);
            SigGens.Instance.SetSdgDc(channelIndex);
            SigGens.Instance.SetSdgParameterOffset(channelIndex, 0.0);
            Utility.GetAndCheckSigGenZero(channelIndex, pathConfig, variables, cancellationToken);
            return Status.Done;
        };
    }
}
