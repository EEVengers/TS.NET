using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class PgaLoadStep : Step
{
    public PgaLoadStep(string name, int channelIndex, BenchCalibrationVariables variables) : base(name)
    {
        // A previous step should set Instruments.Instance.EnableSdgDc(channelIndex) to enable sig gen output
        Action = (CancellationToken cancellationToken) =>
        {
            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, 18, variables);
            var pathConfig = Utility.GetChannelPathConfig(channelIndex, 18, variables);
            Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, variables);

            var zeroValue = Utility.GetAndCheckSigGenZero(channelIndex, pathConfig, variables, cancellationToken);

            Instruments.Instance.SetThunderscopeChannel([channelIndex]);
            var reference = RunAtSampleRate(1_000_000_000, zeroValue, pathConfig, channelIndex, variables, cancellationToken);
            var vpp660M_1Ch = RunAtSampleRate(660_000_000, zeroValue, pathConfig, channelIndex, variables, cancellationToken);
            var vpp500M_1Ch = RunAtSampleRate(500_000_000, zeroValue, pathConfig, channelIndex, variables, cancellationToken);
            var vpp330M_1Ch = RunAtSampleRate(330_000_000, zeroValue, pathConfig, channelIndex, variables, cancellationToken);
            var vpp250M_1Ch = RunAtSampleRate(250_000_000, zeroValue, pathConfig, channelIndex, variables, cancellationToken);
            var vpp165M_1Ch = RunAtSampleRate(165_000_000, zeroValue, pathConfig, channelIndex, variables, cancellationToken);
            var vpp100M_1Ch = RunAtSampleRate(100_000_000, zeroValue, pathConfig, channelIndex, variables, cancellationToken);

            int[] channelIndices_2Ch = [];
            int[] channelIndices_3Ch = [];
            switch (channelIndex)
            {
                case 0:
                    channelIndices_2Ch = [0, 1];
                    channelIndices_3Ch = [0, 1, 2];
                    break;
                case 1:
                    channelIndices_2Ch = [1, 2];
                    channelIndices_3Ch = [1, 2, 3];
                    break;
                case 2:
                    channelIndices_2Ch = [2, 3];
                    channelIndices_3Ch = [2, 3, 0];
                    break;
                case 3:
                    channelIndices_2Ch = [3, 0];
                    channelIndices_3Ch = [3, 0, 1];
                    break;
                default:
                    throw new NotImplementedException();
            }

            Instruments.Instance.SetThunderscopeChannel(channelIndices_2Ch);
            var vpp500M_2Ch = RunAtSampleRate(500_000_000, zeroValue, pathConfig, channelIndex, variables, cancellationToken);
            var vpp330M_2Ch = RunAtSampleRate(330_000_000, zeroValue, pathConfig, channelIndex, variables, cancellationToken);
            var vpp250M_2Ch = RunAtSampleRate(250_000_000, zeroValue, pathConfig, channelIndex, variables, cancellationToken);
            var vpp165M_2Ch = RunAtSampleRate(165_000_000, zeroValue, pathConfig, channelIndex, variables, cancellationToken);
            var vpp100M_2Ch = RunAtSampleRate(100_000_000, zeroValue, pathConfig, channelIndex, variables, cancellationToken);

            Instruments.Instance.SetThunderscopeChannel(channelIndices_3Ch);
            var vpp250M_3Ch = RunAtSampleRate(250_000_000, zeroValue, pathConfig, channelIndex, variables, cancellationToken);
            var vpp165M_3Ch = RunAtSampleRate(165_000_000, zeroValue, pathConfig, channelIndex, variables, cancellationToken);
            var vpp100M_3Ch = RunAtSampleRate(100_000_000, zeroValue, pathConfig, channelIndex, variables, cancellationToken);

            Instruments.Instance.SetThunderscopeChannel([0, 1, 2, 3]);
            var vpp250M_4Ch = RunAtSampleRate(250_000_000, zeroValue, pathConfig, channelIndex, variables, cancellationToken);
            var vpp165M_4Ch = RunAtSampleRate(165_000_000, zeroValue, pathConfig, channelIndex, variables, cancellationToken);
            var vpp100M_4Ch = RunAtSampleRate(100_000_000, zeroValue, pathConfig, channelIndex, variables, cancellationToken);

            List<ThunderscopePgaLoadScale> pgaLoadScales = [];
            pgaLoadScales.Add(new ThunderscopePgaLoadScale() { SampleRate = 1_000_000_000, ChannelCount = 1, Scale = 1.0 });
            pgaLoadScales.Add(new ThunderscopePgaLoadScale() { SampleRate = 660_000_000, ChannelCount = 1, Scale = Math.Round(reference / vpp660M_1Ch, 4) });
            pgaLoadScales.Add(new ThunderscopePgaLoadScale() { SampleRate = 500_000_000, ChannelCount = 1, Scale = Math.Round(reference / vpp500M_1Ch, 4) });
            pgaLoadScales.Add(new ThunderscopePgaLoadScale() { SampleRate = 330_000_000, ChannelCount = 1, Scale = Math.Round(reference / vpp330M_1Ch, 4) });
            pgaLoadScales.Add(new ThunderscopePgaLoadScale() { SampleRate = 250_000_000, ChannelCount = 1, Scale = Math.Round(reference / vpp250M_1Ch, 4) });
            pgaLoadScales.Add(new ThunderscopePgaLoadScale() { SampleRate = 165_000_000, ChannelCount = 1, Scale = Math.Round(reference / vpp165M_1Ch, 4) });
            pgaLoadScales.Add(new ThunderscopePgaLoadScale() { SampleRate = 100_000_000, ChannelCount = 1, Scale = Math.Round(reference / vpp100M_1Ch, 4) });

            pgaLoadScales.Add(new ThunderscopePgaLoadScale() { SampleRate = 500_000_000, ChannelCount = 2, Scale = Math.Round(reference / vpp500M_2Ch, 4) });
            pgaLoadScales.Add(new ThunderscopePgaLoadScale() { SampleRate = 330_000_000, ChannelCount = 2, Scale = Math.Round(reference / vpp330M_2Ch, 4) });
            pgaLoadScales.Add(new ThunderscopePgaLoadScale() { SampleRate = 250_000_000, ChannelCount = 2, Scale = Math.Round(reference / vpp250M_2Ch, 4) });
            pgaLoadScales.Add(new ThunderscopePgaLoadScale() { SampleRate = 165_000_000, ChannelCount = 2, Scale = Math.Round(reference / vpp165M_2Ch, 4) });
            pgaLoadScales.Add(new ThunderscopePgaLoadScale() { SampleRate = 100_000_000, ChannelCount = 2, Scale = Math.Round(reference / vpp100M_2Ch, 4) });

            pgaLoadScales.Add(new ThunderscopePgaLoadScale() { SampleRate = 250_000_000, ChannelCount = 3, Scale = Math.Round(reference / vpp250M_3Ch, 4) });
            pgaLoadScales.Add(new ThunderscopePgaLoadScale() { SampleRate = 165_000_000, ChannelCount = 3, Scale = Math.Round(reference / vpp165M_3Ch, 4) });
            pgaLoadScales.Add(new ThunderscopePgaLoadScale() { SampleRate = 100_000_000, ChannelCount = 3, Scale = Math.Round(reference / vpp100M_3Ch, 4) });

            pgaLoadScales.Add(new ThunderscopePgaLoadScale() { SampleRate = 250_000_000, ChannelCount = 4, Scale = Math.Round(reference / vpp250M_4Ch, 4) });
            pgaLoadScales.Add(new ThunderscopePgaLoadScale() { SampleRate = 165_000_000, ChannelCount = 4, Scale = Math.Round(reference / vpp165M_4Ch, 4) });
            pgaLoadScales.Add(new ThunderscopePgaLoadScale() { SampleRate = 100_000_000, ChannelCount = 4, Scale = Math.Round(reference / vpp100M_4Ch, 4) });

            switch (channelIndex)
            {
                case 0:
                    variables.Calibration.Channel1.PgaLoadScales = pgaLoadScales.ToArray();
                    break;
                case 1:
                    variables.Calibration.Channel2.PgaLoadScales = pgaLoadScales.ToArray();
                    break;
                case 2:
                    variables.Calibration.Channel3.PgaLoadScales = pgaLoadScales.ToArray();
                    break;
                case 3:
                    variables.Calibration.Channel4.PgaLoadScales = pgaLoadScales.ToArray();
                    break;
            }
            variables.ParametersSet += 11;

            Logger.Instance.Log(LogLevel.Information, Index, Status.Passed, $"Config index: {18}");

            Instruments.Instance.SetThunderscopeRate(1000000000, variables);
            return Status.Passed;
        };
    }

    private static double RunAtSampleRate(uint sampleRateHz, double zeroValue, ChannelPathConfig config, int channelIndex, CalibrationVariables variables, CancellationToken cancellationToken)
    {
        Instruments.Instance.SetThunderscopeRate(sampleRateHz, variables);
        return Utility.FindVpp(channelIndex, config, zeroValue, cancellationToken);
    }
}
