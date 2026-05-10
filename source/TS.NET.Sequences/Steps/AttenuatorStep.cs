using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class AttenuatorStep : Step
{
    public AttenuatorStep(string name, int channelIndex, CalibrationVariables variables) : base(name)
    {
        // A previous step should set Instruments.Instance.EnableSdgDc(channelIndex) to enable sig gen output
        Action = (CancellationToken cancellationToken) =>
        {
            if (!variables.TrimDacZeroCalibrated)
            {
                throw new TestbenchException($"Trim DAC zero must be calibrated before running {nameof(AttenuatorStep)}");
            }

            const uint sampleRateHz = 1_000_000_000;
            Instruments.Instance.SetThunderscopeChannel([channelIndex]);
            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(sampleRateHz);
            SigGens.Instance.SetSdgChannel([channelIndex]);

            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, PgaPreampGain.Low, pgaLadder: 7, variables);
            var temperature = Instruments.Instance.GetThunderscopeFpgaTemp();
            var trimDacZero = Frontend.GetTrimDacZero(temperature, pathCalibration.TrimDacZeroM, pathCalibration.TrimDacZeroC);


            const uint frequencyHz = 1000;
            SigGens.Instance.SetSdgChannel([channelIndex]);
            SigGens.Instance.SetSdgSine(channelIndex);
            SigGens.Instance.SetSdgParameterFrequency(channelIndex, frequencyHz);
            SigGens.Instance.SetSdgParameterOffset(channelIndex, 0);
            Thread.Sleep(10);

            
            SigGens.Instance.SetSdgParameterAmplitude(channelIndex, 0.4);
            Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, attenuator: false, trimDacZero, pathCalibration.TrimDPot, pathCalibration.PgaPreampGain, pathCalibration.PgaLadder, ThunderscopeBandwidth.Bw20M, variables.FrontEndSettlingTimeMs);
            var adcPpNoAttenuator = Instruments.Instance.GetThunderscopeAdcPeakPeakAtFrequencyLsq(channelIndex, frequencyHz, sampleRateHz);
            SigGens.Instance.SetSdgParameterAmplitude(channelIndex, 20.0);
            Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, attenuator: true, trimDacZero, pathCalibration.TrimDPot, pathCalibration.PgaPreampGain, pathCalibration.PgaLadder, ThunderscopeBandwidth.Bw20M, variables.FrontEndSettlingTimeMs);
            var adcPpAttenuator = Instruments.Instance.GetThunderscopeAdcPeakPeakAtFrequencyLsq(channelIndex, frequencyHz, sampleRateHz);
            var scale = Math.Round(50.0 * (adcPpNoAttenuator / adcPpAttenuator), 3);

            if (scale > 55 || scale < 45)
            {
                Result!.Summary = $"Scale: {scale:F3}";
                Logger.Instance.Log(LogLevel.Information, Index, Status.Failed, $"Attenuator scale outside limits: {scale:F3}");
                return Status.Failed;
            }

            variables.Calibration.Frontend[channelIndex].AttenuatorScale = scale;
            variables.ParametersSet++;

            Result!.Summary = $"Scale: {scale:F3}";
            Logger.Instance.Log(LogLevel.Information, Index, Status.Passed, $"Attenuator scale: {scale:F3}");
            return Status.Passed;
        };
    }
}