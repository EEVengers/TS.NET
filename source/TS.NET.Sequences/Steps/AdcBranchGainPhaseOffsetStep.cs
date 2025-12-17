using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class AdcBranchGainPhaseOffsetStep : Step
{
    public AdcBranchGainPhaseOffsetStep(string name, int channelIndex, uint rateHz, PgaPreampGain pgaGain, int pgaLadder, BenchCalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.SetThunderscopeChannel([channelIndex]);
            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(rateHz);
            Instruments.Instance.SetThunderscopeAdcCalibration([0, 0, 0, 0, 0, 0, 0, 0]);        // Reset to all zero

            // First set the maximum range
            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, pgaGain, pgaLadder, variables);
            Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, variables.FrontEndSettlingTimeMs);

            var frequencyHz = (uint)(rateHz / 1000.0);
            SigGens.Instance.SetSdgChannel([channelIndex]);
            SigGens.Instance.SetSdgSine(channelIndex);
            SigGens.Instance.SetSdgParameterAmplitude(channelIndex, pathCalibration.BufferInputVpp * 0.9);
            SigGens.Instance.SetSdgParameterFrequency(channelIndex, frequencyHz);
            SigGens.Instance.SetSdgParameterOffset(channelIndex, 0);
            
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
            
            void GetBranchData(out double[] normalisedAmplitudeScales, out double[] normalisedSkews, out double[] normalisedOffsets)
            {
                normalisedAmplitudeScales = new double[8];
                normalisedSkews = new double[8];
                normalisedOffsets = new double[8];
                Instruments.Instance.GetThunderscopeFineBranchesSine(frequencyHz, rateHz, pathCalibration.BufferInputVpp, AdcResolution.EightBit, out var amplitudes, out var phases, out var offsets);
                var minAmplitude = amplitudes.Min();
                var maxAmplitude = amplitudes.Max();
                var midrangeAmplitude = minAmplitude + ((maxAmplitude - minAmplitude) / 2.0);
                //var minPhase = phases.Min();
                //var maxPhase = phases.Max();
                //var midrangePhase = minPhase + ((maxPhase - minPhase) / 2.0);
                var minOffset = offsets.Min();
                var maxOffset = offsets.Max();
                var midrangeOffset = minOffset + ((maxOffset - minOffset) / 2.0);
                for (int i = 0; i < 8; i++)
                {
                    var sampleOrder = i switch
                    {
                        0 => 0,
                        1 => 2,
                        2 => 5,
                        3 => 7,
                        4 => 3,
                        5 => 1,
                        6 => 6,
                        7 => 4,
                    };
                    var samplesPerCycle = rateHz/frequencyHz;
                    var degreesPerSample = 360.0/samplesPerCycle;
                    var expectedPhaseOffset = sampleOrder * degreesPerSample;
                    normalisedAmplitudeScales[i] = (amplitudes[i] / midrangeAmplitude) - 1.0;
                    normalisedSkews[i] = (phases[i] - phases[0]) - expectedPhaseOffset;
                    normalisedOffsets[i] = offsets[i] - midrangeOffset;
                    Logger.Instance.Log(LogLevel.Information, Index, $"Branch {i + 1}: {(normalisedAmplitudeScales[i] * 100.0):+0.000;-0.000}%");
                }
            }
        };
    }
}
