using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class FindSigGenZeroStep : Step
{
    // channelIndex is both sig gen channel and thunderscope channel.
    public FindSigGenZeroStep(string name, int channelIndex, BenchCalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            if (!variables.TrimDacZeroCalibrated)
            {
                throw new TestbenchException($"Trim DAC zero must be calibrated before running {nameof(FindSigGenZeroStep)}");
            }

            Instruments.Instance.SetThunderscopeChannel([channelIndex]);
            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(1_000_000_000);

            var pathData = Utility.GetChannelPathData(channelIndex, PgaPreampGain.High, pgaLadder: 0, variables);
            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, PgaPreampGain.High, pgaLadder: 0, variables);
            var temperature = Instruments.Instance.GetThunderscopeFpgaTemp();
            var trimDacZero = Frontend.GetTrimDacZero(temperature, pathCalibration.TrimDacZeroM, pathCalibration.TrimDacZeroC);
            Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, trimDacZero, pathCalibration.TrimDPot, pathCalibration.PgaPreampGain, pathCalibration.PgaLadder, variables.FrontEndSettlingTimeMs);
            SigGens.Instance.SetSdgChannel([channelIndex]);
            SigGens.Instance.SetSdgDc(channelIndex);
            SigGens.Instance.SetSdgParameterOffset(channelIndex, 0.0);
            Utility.GetAndCheckSigGenZero(channelIndex, pathData, variables, cancellationToken);
            Result!.Summary = $"{variables.SigGenZero:F4} V";
            return Status.Done;
        };
    }
}
