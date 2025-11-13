using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class AdcBranchGainsStep : Step
{
    public AdcBranchGainsStep(string name, int channelIndex, uint rateHz, BenchCalibrationVariables variables) : base(name)
    {
        // A previous step should set Instruments.Instance.EnableSdgDc(channelIndex) to enable sig gen output
        Action = (CancellationToken cancellationToken) =>
        {
            var pathIndex = 21;
            var branchFineGains = new byte[8];
            var branchScales = new double[8];

            Instruments.Instance.SetThunderscopeChannel([channelIndex]);
            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(rateHz);
            Instruments.Instance.SetThunderscopeAdcCalibration([0, 0, 0, 0, 0, 0, 0, 0]);        // Reset to all zero

            Instruments.Instance.SetSdgChannel([channelIndex]);
            Instruments.Instance.SetSdgNoise(channelIndex, 0.16, 0.0);

            // First set the maximum range
            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, pathIndex, variables);
            var pathConfig = Utility.GetChannelPathConfig(channelIndex, pathIndex, variables);
            Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, variables);

            GetBranchScales();

            for (int i = 0; i < 8; i++)
            {
                // Compute the deviation from 1.0 in LSBs of 1/8192 (approx 8181.8181 per unit)
                // If branchScale < 1, the delta is positive; if > 1, delta is negative (two's complement via ~ and & 0x7F)

                int gainSetting;
                if (branchScales[i] < 1.0)
                {
                    gainSetting = (int)Math.Round((1.0 - branchScales[i]) * 8181.8181);
                }
                else
                {
                    gainSetting = (int)Math.Round((branchScales[i] - 1.0) * 8181.8181);
                    gainSetting = ~gainSetting;
                }
                if (gainSetting > 63)
                    gainSetting = 63;
                if (gainSetting < -64)
                    gainSetting = -64;
                //Logger.Instance.Log(LogLevel.Information, Index, $"Branch {i + 1}: {(branchScales[i] * 100.0) - 100.0:+0.000;-0.000}%, fgain_branchx<6:0>: {gainSetting}");
                branchFineGains[i] = (byte)(gainSetting & 0x7F);
            }

            List<string> deviationBefore = [
                $"{(branchScales[0] * 100.0) - 100.0:+0.000;-0.000}%",
                $"{(branchScales[1] * 100.0) - 100.0:+0.000;-0.000}%",
                $"{(branchScales[2] * 100.0) - 100.0:+0.000;-0.000}%",
                $"{(branchScales[3] * 100.0) - 100.0:+0.000;-0.000}%",
                $"{(branchScales[4] * 100.0) - 100.0:+0.000;-0.000}%",
                $"{(branchScales[5] * 100.0) - 100.0:+0.000;-0.000}%",
                $"{(branchScales[6] * 100.0) - 100.0:+0.000;-0.000}%",
                $"{(branchScales[7] * 100.0) - 100.0:+0.000;-0.000}%"
                ];

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
            GetBranchScales();

            Result!.Metadata!.Add(new ResultMetadataTable()
            {
                Name = "ADC branch gain % deviation from midrange, before & after adjustment",
                ShowInReport = true,
                Headers = ["Branch", "Before", "After"],
                Rows = [
                    ["1", deviationBefore[0], $"{(branchScales[0] * 100.0) - 100.0:+0.000;-0.000}%"],
                    ["2", deviationBefore[1], $"{(branchScales[1] * 100.0) - 100.0:+0.000;-0.000}%"],
                    ["3", deviationBefore[2], $"{(branchScales[2] * 100.0) - 100.0:+0.000;-0.000}%"],
                    ["4", deviationBefore[3], $"{(branchScales[3] * 100.0) - 100.0:+0.000;-0.000}%"],
                    ["5", deviationBefore[4], $"{(branchScales[4] * 100.0) - 100.0:+0.000;-0.000}%"],
                    ["6", deviationBefore[5], $"{(branchScales[5] * 100.0) - 100.0:+0.000;-0.000}%"],
                    ["7", deviationBefore[6], $"{(branchScales[6] * 100.0) - 100.0:+0.000;-0.000}%"],
                    ["8", deviationBefore[7], $"{(branchScales[7] * 100.0) - 100.0:+0.000;-0.000}%"],
                ]
            });

            //Instruments.Instance.SetThunderscopeAdcCalibration([0, 0, 0, 0, 0, 0, 0, 0]);
            Instruments.Instance.SetSdgChannel([]);
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done);
            //Instruments.Instance.SetSdgDc(channelIndex);
            return Status.Done;

            void GetBranchScales()
            {
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
                //var meanStdev = sum / 8.0;
                for (int i = 0; i < 8; i++)
                {
                    branchScales[i] = branchStdev[i] / middleStdev;
                    Logger.Instance.Log(LogLevel.Information, Index, $"Branch {i + 1}: {(branchScales[i] * 100.0) - 100.0:+0.000;-0.000}%");
                }
            }
        };
    }
}
