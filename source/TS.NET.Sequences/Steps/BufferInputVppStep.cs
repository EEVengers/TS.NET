using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class BufferInputVppStep : Step
{
    public BufferInputVppStep(string name, int channelIndex, PgaPreampGain pgaGain, int pgaLadder, BenchCalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.SetThunderscopeChannel([channelIndex]);
            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(1_000_000_000);

            Instruments.Instance.SetSdgChannel([channelIndex]);
            Instruments.Instance.SetSdgDc(channelIndex);
            Instruments.Instance.SetSdgParameterOffset(channelIndex, 0);

            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, pgaGain, pgaLadder, variables);
            var pathConfig = Utility.GetChannelPathConfig(channelIndex, pgaGain, pgaLadder, variables);
            Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, variables.FrontEndSettlingTimeMs);
            
            var zeroValue = Utility.GetAndCheckSigGenZero(channelIndex, pathConfig, variables, cancellationToken);
            var vpp = Utility.FindVpp(channelIndex, pathConfig, zeroValue, cancellationToken);

            pathCalibration.BufferInputVpp = Math.Round(vpp, 4);
            variables.ParametersSet += 2;

            Result!.Summary = $"Vpp: {vpp:F4}";
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Vpp: {vpp:F4}");
            return Status.Passed;
        };
    }
}
