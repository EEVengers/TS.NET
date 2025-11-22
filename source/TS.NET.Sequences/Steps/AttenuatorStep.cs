using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class AttenuatorStep : Step
{
    public AttenuatorStep(string name, int channelIndex, CalibrationVariables variables) : base(name)
    {
        // A previous step should set Instruments.Instance.EnableSdgDc(channelIndex) to enable sig gen output
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.SetThunderscopeChannel([channelIndex]);
            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(1_000_000_000);

            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, 18, variables);

            Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, attenuator: true, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, ThunderscopeBandwidth.Bw20M, variables.FrontEndSettlingTimeMs);
            SigGens.Instance.SetSdgParameterOffset(channelIndex, 10);
            Thread.Sleep(100);
            var max = Instruments.Instance.GetThunderscopeAverage(channelIndex, sampleCount: 10_000_000);
            SigGens.Instance.SetSdgParameterOffset(channelIndex, -10);
            Thread.Sleep(100);
            var min = Instruments.Instance.GetThunderscopeAverage(channelIndex, sampleCount: 10_000_000);
            SigGens.Instance.SetSdgParameterOffset(channelIndex, 0);

            var voltage = ((max - min) / 256.0) * pathCalibration.BufferInputVpp;
            var scale = voltage / 20.0;

            scale = Math.Round(scale, 6);

            if (scale > 0.025 || scale < 0.015)
            {
                Logger.Instance.Log(LogLevel.Information, Index, Status.Failed, $"Attenuator scale outside limits: {scale}");
                return Status.Failed;
            }

            switch (channelIndex)
            {
                case 0:
                    variables.Calibration.Channel1.AttenuatorScale = scale;
                    variables.ParametersSet++;
                    break;
                case 1:
                    variables.Calibration.Channel2.AttenuatorScale = scale;
                    variables.ParametersSet++;
                    break;
                case 2:
                    variables.Calibration.Channel3.AttenuatorScale = scale;
                    variables.ParametersSet++;
                    break;
                case 3:
                    variables.Calibration.Channel4.AttenuatorScale = scale;
                    variables.ParametersSet++;
                    break;
            }

            Result!.Summary = $"Scale: {scale}";
            Logger.Instance.Log(LogLevel.Information, Index, Status.Passed, $"Attenuator scale: {scale}");
            return Status.Passed;
        };
    }
}