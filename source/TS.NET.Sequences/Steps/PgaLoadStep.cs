using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class PgaLoadStep : Step
{
    public PgaLoadStep(string name, int channelIndex, int[] channelIndices, uint sampleRateHz, BenchCalibrationVariables variables) : base(name)
    {
        // A previous step should set Instruments.Instance.EnableSdgDc(channelIndex) to enable sig gen output
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.SetThunderscopeChannel(channelIndices, false);
            Instruments.Instance.SetThunderscopeRate(sampleRateHz, variables);

            var configIndex = 21;
            var pathConfig = Utility.GetChannelPathConfig(channelIndex, configIndex, variables);
            var vpp = Utility.FindVpp(channelIndex, pathConfig, variables.SigGenZero, cancellationToken);
            var scaleValue = Math.Round(variables.ReferenceVpp / vpp, 4);
            var scale = new ThunderscopePgaLoadScale() { SampleRate = sampleRateHz, ChannelCount = (byte)channelIndices.Length, Scale = scaleValue };
            switch (channelIndex)
            {
                case 0:
                    variables.Calibration.Channel1.PgaLoadScales = [.. variables.Calibration.Channel1.PgaLoadScales, scale];
                    break;
                case 1:
                    variables.Calibration.Channel2.PgaLoadScales = [.. variables.Calibration.Channel2.PgaLoadScales, scale];
                    break;
                case 2:
                    variables.Calibration.Channel3.PgaLoadScales = [.. variables.Calibration.Channel3.PgaLoadScales, scale];
                    break;
                case 3:
                    variables.Calibration.Channel4.PgaLoadScales = [.. variables.Calibration.Channel4.PgaLoadScales, scale];
                    break;
            }
            variables.ParametersSet += 1;

            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Scale: {scaleValue}");
            return Status.Done;
        };
    }
}
