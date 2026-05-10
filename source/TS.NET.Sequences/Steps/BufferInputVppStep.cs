using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class BufferInputVppStep : Step
{
    public BufferInputVppStep(string name, int channelIndex, PgaPreampGain pgaGain, byte pgaLadder, BenchCalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            if (!variables.TrimDacZeroCalibrated)
            {
                throw new TestbenchException($"Trim DAC zero must be calibrated before running {nameof(BufferInputVppStep)}");
            }

            const uint sampleRateHz = 1_000_000_000;
            Instruments.Instance.SetThunderscopeChannel([channelIndex]);
            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(sampleRateHz);

            SigGens.Instance.SetSdgChannel([channelIndex]);
            //SigGens.Instance.SetSdgDc(channelIndex);
            //SigGens.Instance.SetSdgParameterOffset(channelIndex, 0);

            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, pgaGain, pgaLadder, variables);
            var temperature = Instruments.Instance.GetThunderscopeFpgaTemp();
            var trimDacZero = Frontend.GetTrimDacZero(temperature, pathCalibration.TrimDacZeroM, pathCalibration.TrimDacZeroC);
            Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, trimDacZero, pathCalibration.TrimDPot, pathCalibration.PgaPreampGain, pathCalibration.PgaLadder, variables.FrontEndSettlingTimeMs);
            
            var pathConfig = Utility.GetChannelPathData(channelIndex, pgaGain, pgaLadder, variables);
            //var zeroValue = Utility.GetAndCheckSigGenZero(channelIndex, pathConfig, variables, cancellationToken);        // Causes relay range switching in sig-gen
            var vpp = Utility.FindVpp(channelIndex, pathConfig, variables.SigGenZero, sampleRateHz, cancellationToken);

            pathCalibration.BufferInputVpp = Math.Round(vpp, 4);
            variables.ParametersSet++;

            Result!.Summary = $"Vpp: {vpp:F4}";
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Vpp: {vpp:F4}");
            return Status.Passed;
        };
    }
}
