using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class AdcFineGainStep : Step
{
    public AdcFineGainStep(string name, BenchCalibrationVariables variables) : base(name)
    {
        // A previous step should set Instruments.Instance.EnableSdgDc(channelIndex) to enable sig gen output
        Action = (CancellationToken cancellationToken) =>
        {
            var channelIndex = 0;
            var pathIndex = 21;
            // First set the maximum range
            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, pathIndex, variables);
            var pathConfig = Utility.GetChannelPathConfig(channelIndex, pathIndex, variables);
            Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, variables);
            Instruments.Instance.SetSdgNoise(channelIndex, 0.16, 0.0);
            Instruments.Instance.SetThunderscopeChannel([channelIndex]);

            Instruments.Instance.GetThunderscopeFineBranches(out var branchMean, out var branchStdev);
            var minStdev = double.MaxValue;
            var maxStdev = double.MinValue;
            var sum = 0.0;
            foreach (var stdev in branchStdev)
            {
                if (stdev > maxStdev)
                    maxStdev = stdev;
                if (stdev < minStdev)
                    minStdev = stdev;
                sum += stdev;
            }

            var middleStdev = minStdev + ((maxStdev - minStdev) / 2.0);
            var meanStdev = sum / 8.0;

            var branchFineGains = new byte[8];
            for (int i = 0; i < 8; i++)
            {
                // Compute the deviation from 1.0 in LSBs of 1/8192 (approx 8181.8181 per unit)
                // If branchScale < 1, the delta is positive; if > 1, delta is negative (two's complement via ~ and & 0x7F)
                //double branchScale = branchStdev[i] / meanStdev;
                double branchScale = branchStdev[i] / middleStdev;

                int gainSetting;
                if (branchScale < 1.0)
                {
                    gainSetting = (int)Math.Round((1.0 - branchScale) * 8181.8181);
                }
                else
                {
                    gainSetting = (int)Math.Round((branchScale - 1.0) * 8181.8181);
                    gainSetting = ~gainSetting;
                }
                if (gainSetting > 63)
                    gainSetting = 63;
                if (gainSetting < -64)
                    gainSetting = -64;
                Logger.Instance.Log(LogLevel.Information, Index, $"Branch {i + 1}: {(branchScale*100.0)-100.0:+0.000;-0.000}%, fgain_branchx<6:0>: {gainSetting}");
                branchFineGains[i] = (byte)(gainSetting & 0x7F);
            }
            Instruments.Instance.SetThunderscopeAdcCalibration(branchFineGains);
            variables.Calibration.Adc.FineGainBranch1 = branchFineGains[0];
            variables.Calibration.Adc.FineGainBranch2 = branchFineGains[1];
            variables.Calibration.Adc.FineGainBranch3 = branchFineGains[2];
            variables.Calibration.Adc.FineGainBranch4 = branchFineGains[3];
            variables.Calibration.Adc.FineGainBranch5 = branchFineGains[4];
            variables.Calibration.Adc.FineGainBranch6 = branchFineGains[5];
            variables.Calibration.Adc.FineGainBranch7 = branchFineGains[6];
            variables.Calibration.Adc.FineGainBranch8 = branchFineGains[7];
            variables.ParametersSet += 8;

            // Now validate
            Instruments.Instance.GetThunderscopeFineBranches(out branchMean, out branchStdev);
            minStdev = double.MaxValue;
            maxStdev = double.MinValue;
            sum = 0.0;
            foreach (var stdev in branchStdev)
            {
                if (stdev > maxStdev)
                    maxStdev = stdev;
                if (stdev < minStdev)
                    minStdev = stdev;
                sum += stdev;
            }

            meanStdev = sum / 8.0;
            for (int i = 0; i < 8; i++)
            {
                double branchScale = branchStdev[i] / meanStdev;
                Logger.Instance.Log(LogLevel.Information, Index, $"Branch {i + 1}: {(branchScale*100.0)-100.0:+0.000;-0.000}%");
            }

            Logger.Instance.Log(LogLevel.Information, Index, Status.Passed, $"");

            Instruments.Instance.SetSdgDc(channelIndex);
            return Status.Passed;
        };
    }
}
