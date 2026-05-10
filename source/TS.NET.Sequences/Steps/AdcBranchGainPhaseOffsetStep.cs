using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class AdcBranchGainPhaseOffsetStep : Step
{
    public AdcBranchGainPhaseOffsetStep(string name, int[] channelIndices, uint rateHz, PgaPreampGain pgaGain, int pgaLadder, BenchCalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            if (!variables.TrimDacZeroCalibrated)
            {
                throw new TestbenchException($"Trim DAC zero must be calibrated before running {nameof(AdcBranchGainPhaseOffsetStep)}");
            }
            if (!variables.BufferInputVppCalibrated)
            {
                throw new TestbenchException($"Buffer input Vpp must be calibrated before running {nameof(AdcBranchGainPhaseOffsetStep)}");
            }

            Instruments.Instance.SetThunderscopeChannel(channelIndices);
            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(rateHz);
            Instruments.Instance.SetThunderscopeBranchGains([0, 0, 0, 0, 0, 0, 0, 0]);        // Reset to all zero

            //var frequencyHz = (uint)(rateHz / 1000.0);
            uint frequencyHz = 1000;
            SigGens.Instance.SetSdgChannel(channelIndices);

            // Setup the frontend for each channel
            foreach (var channelIndex in channelIndices)
            {
                var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, pgaGain, pgaLadder, variables);
                var temperature = Instruments.Instance.GetThunderscopeFpgaTemp();
                var trimDacZero = Frontend.GetTrimDacZero(temperature, pathCalibration.TrimDacZeroM, pathCalibration.TrimDacZeroC);
                Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, trimDacZero, pathCalibration.TrimDPot, pathCalibration.PgaPreampGain, pathCalibration.PgaLadder, variables.FrontEndSettlingTimeMs);

                SigGens.Instance.SetSdgSine(channelIndex);
                SigGens.Instance.SetSdgParameterAmplitude(channelIndex, pathCalibration.BufferInputVpp * 0.8);
                SigGens.Instance.SetSdgParameterFrequency(channelIndex, frequencyHz);
                SigGens.Instance.SetSdgParameterOffset(channelIndex, 0);
            }

            GetBranchData(out var branchScalesBefore, out var normalisedPhasesBefore, out var normalisedOffsetsBefore);

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

            Instruments.Instance.SetThunderscopeBranchGains(branchFineGains);
            variables.Calibration.Adc.BranchGain.First(fg => fg.Channel.SequenceEqual(channelIndices)).RateGain.First(rfg => rfg.Rate == rateHz).Gain = gainSettings;
            variables.ParametersSet += 8;

            GetBranchData(out var branchScalesAfter, out var normalisedPhasesAfter, out var normalisedOffsetsAfter);

            /*Result!.Metadata!.Add(new ResultMetadataTable()
            {
                Name = "ADC branch gain, time skew & DC offset deviation, before fine gain adjustment",
                ShowInReport = true,
                Headers = ["Branch", "Gain", "Time skew", "DC offset"],
                Rows = [
                    ["1", FormatDev(branchScalesBefore[0]), $"{normalisedPhasesBefore[0] / 360.0 / frequencyHz * 1e12:F1}ps", $"{normalisedOffsetsBefore[0] * 1e3:F1}mV"],
                    ["2", FormatDev(branchScalesBefore[1]), $"{normalisedPhasesBefore[1] / 360.0 / frequencyHz * 1e12:F1}ps", $"{normalisedOffsetsBefore[1] * 1e3:F1}mV"],
                    ["3", FormatDev(branchScalesBefore[2]), $"{normalisedPhasesBefore[2] / 360.0 / frequencyHz * 1e12:F1}ps", $"{normalisedOffsetsBefore[2] * 1e3:F1}mV"],
                    ["4", FormatDev(branchScalesBefore[3]), $"{normalisedPhasesBefore[3] / 360.0 / frequencyHz * 1e12:F1}ps", $"{normalisedOffsetsBefore[3] * 1e3:F1}mV"],
                    ["5", FormatDev(branchScalesBefore[4]), $"{normalisedPhasesBefore[4] / 360.0 / frequencyHz * 1e12:F1}ps", $"{normalisedOffsetsBefore[4] * 1e3:F1}mV"],
                    ["6", FormatDev(branchScalesBefore[5]), $"{normalisedPhasesBefore[5] / 360.0 / frequencyHz * 1e12:F1}ps", $"{normalisedOffsetsBefore[5] * 1e3:F1}mV"],
                    ["7", FormatDev(branchScalesBefore[6]), $"{normalisedPhasesBefore[6] / 360.0 / frequencyHz * 1e12:F1}ps", $"{normalisedOffsetsBefore[6] * 1e3:F1}mV"],
                    ["8", FormatDev(branchScalesBefore[7]), $"{normalisedPhasesBefore[7] / 360.0 / frequencyHz * 1e12:F1}ps", $"{normalisedOffsetsBefore[7] * 1e3:F1}mV"],
                ]
            });*/

            Result!.Metadata!.Add(new ResultMetadataTable()
            {
                Name = "ADC branch gain deviation from midrange, before & after adjustment",
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

            /*Result!.Metadata!.Add(new ResultMetadataTable()
            {
                Name = "ADC branch gain, time skew & DC offset deviation, after fine gain adjustment",
                ShowInReport = true,
                Headers = ["Branch", "Gain", "Time skew", "DC offset"],
                Rows = [
                    ["1", FormatDev(branchScalesAfter[0]), $"{normalisedPhasesAfter[0] / 360.0 / frequencyHz * 1e12:F1}ps", $"{normalisedOffsetsAfter[0] * 1e3:F1}mV"],
                    ["2", FormatDev(branchScalesAfter[1]), $"{normalisedPhasesAfter[1] / 360.0 / frequencyHz * 1e12:F1}ps", $"{normalisedOffsetsAfter[1] * 1e3:F1}mV"],
                    ["3", FormatDev(branchScalesAfter[2]), $"{normalisedPhasesAfter[2] / 360.0 / frequencyHz * 1e12:F1}ps", $"{normalisedOffsetsAfter[2] * 1e3:F1}mV"],
                    ["4", FormatDev(branchScalesAfter[3]), $"{normalisedPhasesAfter[3] / 360.0 / frequencyHz * 1e12:F1}ps", $"{normalisedOffsetsAfter[3] * 1e3:F1}mV"],
                    ["5", FormatDev(branchScalesAfter[4]), $"{normalisedPhasesAfter[4] / 360.0 / frequencyHz * 1e12:F1}ps", $"{normalisedOffsetsAfter[4] * 1e3:F1}mV"],
                    ["6", FormatDev(branchScalesAfter[5]), $"{normalisedPhasesAfter[5] / 360.0 / frequencyHz * 1e12:F1}ps", $"{normalisedOffsetsAfter[5] * 1e3:F1}mV"],
                    ["7", FormatDev(branchScalesAfter[6]), $"{normalisedPhasesAfter[6] / 360.0 / frequencyHz * 1e12:F1}ps", $"{normalisedOffsetsAfter[6] * 1e3:F1}mV"],
                    ["8", FormatDev(branchScalesAfter[7]), $"{normalisedPhasesAfter[7] / 360.0 / frequencyHz * 1e12:F1}ps", $"{normalisedOffsetsAfter[7] * 1e3:F1}mV"],
                ]
            });*/

            //Instruments.Instance.SetThunderscopeAdcCalibration([0, 0, 0, 0, 0, 0, 0, 0]);
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done);
            return Status.Done;

            string FormatDev(double deviation)
            {
                return $"{deviation * 100.0:+0.000;-0.000}%";
            }

            void GetBranchData(out double[] normalisedAmplitudeScales, out double[] normalisedPhases, out double[] normalisedOffsets)
            {
                normalisedAmplitudeScales = new double[8];
                normalisedPhases = new double[8];
                normalisedOffsets = new double[8];

                Instruments.Instance.GetThunderscopeFineBranchesSine(frequencyHz, rateHz, out var amplitudes, out var phases, out var offsets);

                int[][] branchGroups = channelIndices.Length switch
                {
                    1 => [[0, 1, 2, 3, 4, 5, 6, 7]],
                    2 => [[0, 2, 1, 3], [4, 6, 5, 7]],
                    3 => [[0, 1], [2, 3], [4, 5], [6, 7]],
                    4 => [[0, 1], [2, 3], [4, 5], [6, 7]],
                    _ => throw new NotImplementedException()
                };

                foreach (var group in branchGroups)
                {
                    var minAmplitude = double.MaxValue;
                    var maxAmplitude = double.MinValue;
                    var minOffset = double.MaxValue;
                    var maxOffset = double.MinValue;

                    foreach (var branchIndex in group)
                    {
                        var amplitude = amplitudes[branchIndex];
                        var offset = offsets[branchIndex];

                        if (amplitude < minAmplitude) minAmplitude = amplitude;
                        if (amplitude > maxAmplitude) maxAmplitude = amplitude;
                        if (offset < minOffset) minOffset = offset;
                        if (offset > maxOffset) maxOffset = offset;
                    }

                    var midrangeAmplitude = minAmplitude + ((maxAmplitude - minAmplitude) / 2.0);
                    var midrangeOffset = minOffset + ((maxOffset - minOffset) / 2.0);

                    foreach (var branchIndex in group)
                    {
                        normalisedAmplitudeScales[branchIndex] = (amplitudes[branchIndex] / midrangeAmplitude) - 1.0;
                        normalisedOffsets[branchIndex] = offsets[branchIndex] - midrangeOffset;
                    }
                }

                for (int i = 0; i < 8; i++)
                {
                    Logger.Instance.Log(LogLevel.Information, Index, $"Branch {i + 1}: {(normalisedAmplitudeScales[i] * 100.0):+0.000;-0.000}%");
                }

                Console.WriteLine();
            }
        };
    }
}
