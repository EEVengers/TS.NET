using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class PgaLoadSetupStep : Step
{
    public PgaLoadSetupStep(string name, int channelIndex, BenchCalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.SetThunderscopeChannel([channelIndex]);
            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(1_000_000_000);

            Instruments.Instance.SetSdgChannel([channelIndex]);
            Instruments.Instance.SetSdgDc(channelIndex);
            Instruments.Instance.SetSdgParameterOffset(channelIndex, 0);
            cancellationToken.WaitHandle.WaitOne(1000);

            var configIndex = 21;
            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, configIndex, variables);
            var pathConfig = Utility.GetChannelPathConfig(channelIndex, configIndex, variables);
            Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, variables.FrontEndSettlingTimeMs);

            Utility.GetAndCheckSigGenZero(channelIndex, pathConfig, variables, cancellationToken);
            
            variables.ReferenceVpp = Utility.FindVpp(channelIndex, pathConfig, variables.SigGenZero, cancellationToken);

            var scale = new ThunderscopePgaLoadScale() { SampleRate = 1_000_000_000, ChannelCount = 1, Scale = 1 };
            switch (channelIndex)
            {
                case 0:
                    variables.Calibration.Channel1.PgaLoadScales = [scale];
                    break;
                case 1:
                    variables.Calibration.Channel2.PgaLoadScales = [scale];
                    break;
                case 2:
                    variables.Calibration.Channel3.PgaLoadScales = [scale];
                    break;
                case 3:
                    variables.Calibration.Channel4.PgaLoadScales = [scale];
                    break;
            }
            variables.ParametersSet += 1;

            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Config index: {configIndex}");
            return Status.Done;
        };
    }
}
