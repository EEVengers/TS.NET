using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class AdcBranchGainsStep : Step
{
    public AdcBranchGainsStep(string name, int channelIndex, uint rateHz, PgaPreampGain pgaGain, int pgaLadder, BenchCalibrationVariables variables) : base(name)
    {
        // A previous step should set Instruments.Instance.EnableSdgDc(channelIndex) to enable sig gen output
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.SetThunderscopeChannel([channelIndex]);
            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(rateHz);
            Instruments.Instance.SetThunderscopeAdcCalibration([0, 0, 0, 0, 0, 0, 0, 0]);        // Reset to all zero

            SigGens.Instance.SetSdgChannel([channelIndex]);
            SigGens.Instance.SetSdgNoise(channelIndex, 0.16, 0.0);

            // First set the maximum range
            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, pgaGain, pgaLadder, variables);
            Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, variables.FrontEndSettlingTimeMs);

            var branchScalesBefore = GetBranchScales();

            var branchFineGains = new byte[8];
            var gainSettings = new int[8];
            for (int i = 0; i < 8; i++)
            {
                // Compute the deviation from 1.0 in LSBs of 1/8192 (approx 8181.8181 per unit)
                int gainSetting = (int)Math.Round(branchScalesBefore[i] * -8181.8181);
                if (gainSetting > 63)
                    gainSetting = 63;
                if (gainSetting < -64)
                    gainSetting = -64;
                gainSettings[i] = gainSetting;
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

            var branchScalesAfter = GetBranchScales();

            Result!.Metadata!.Add(new ResultMetadataTable()
            {
                Name = "ADC branch gain % deviation from midrange, before & after adjustment",
                ShowInReport = true,
                Headers = ["Branch", "Before", "Setting", "After"],
                Rows = [
                    ["1", FormatDev(branchScalesBefore[0]), gainSettings[0].ToString(), FormatDev(branchScalesAfter[0])],
                    ["2", FormatDev(branchScalesBefore[1]), gainSettings[1].ToString(),  FormatDev(branchScalesAfter[1])],
                    ["3", FormatDev(branchScalesBefore[2]), gainSettings[2].ToString(),  FormatDev(branchScalesAfter[2])],
                    ["4", FormatDev(branchScalesBefore[3]), gainSettings[3].ToString(),  FormatDev(branchScalesAfter[3])],
                    ["5", FormatDev(branchScalesBefore[4]), gainSettings[4].ToString(),  FormatDev(branchScalesAfter[4])],
                    ["6", FormatDev(branchScalesBefore[5]), gainSettings[5].ToString(),  FormatDev(branchScalesAfter[5])],
                    ["7", FormatDev(branchScalesBefore[6]), gainSettings[6].ToString(),  FormatDev(branchScalesAfter[6])],
                    ["8", FormatDev(branchScalesBefore[7]), gainSettings[7].ToString(),  FormatDev(branchScalesAfter[7])]
                ]
            });

            //Instruments.Instance.SetThunderscopeAdcCalibration([0, 0, 0, 0, 0, 0, 0, 0]);
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done);
            return Status.Done;

            string FormatDev(double deviation)
            {
                return $"{deviation * 100.0:+0.000;-0.000}%";
            }
            
            double[] GetBranchScales()
            {
                var branchScales = new double[8];
                Instruments.Instance.GetThunderscopeFineBranches(out var branchMean, out var branchStdev);
                var min = branchStdev.Min();
                var max = branchStdev.Max();
                var midrange = min + ((max - min) / 2.0);
                for (int i = 0; i < 8; i++)
                {
                    branchScales[i] = (branchStdev[i] / midrange) - 1.0;
                    Logger.Instance.Log(LogLevel.Information, Index, $"Branch {i + 1}: {(branchScales[i] * 100.0):+0.000;-0.000}%");
                }
                return branchScales;
            }
        };
    }
}
