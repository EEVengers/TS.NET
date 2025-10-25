using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class AttenuatorStep : Step
{
    public AttenuatorStep(string name, int channelIndex, CalibrationVariables variables) : base(name)
    {
        // A previous step should set Instruments.Instance.EnableSdgDc(channelIndex) to enable sig gen output
        Action = (CancellationToken cancellationToken) =>
        {
            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, 18, variables);
            //var pathConfig = Utility.GetChannelPathConfig(channelIndex, 18);

            Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, attenuator: true, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, variables);
            Instruments.Instance.SetSdgOffset(channelIndex, 10);
            Thread.Sleep(1000);
            var max = Instruments.Instance.GetThunderscopeAverage(channelIndex);
            Instruments.Instance.SetSdgOffset(channelIndex, -10);
            Thread.Sleep(1000);
            var min = Instruments.Instance.GetThunderscopeAverage(channelIndex);
            Instruments.Instance.SetSdgOffset(channelIndex, 0);

            var voltage = ((max - min) / 255.0) * pathCalibration.BufferInputVpp;
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

            Logger.Instance.Log(LogLevel.Information, Index, Status.Passed, $"Attenuator scale: {scale}");
            return Status.Passed;
        };
    }
}