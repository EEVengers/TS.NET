using Microsoft.Extensions.Logging;

namespace TS.NET;

public static class Frontend
{
    public static ushort GetTrimDacZero(double temperature, double trimDacZeroM, double trimDacZeroC)
    {
        var dacCode = (trimDacZeroM * temperature) + trimDacZeroC;
        if (dacCode < 0)
            dacCode = 0;
        else if (dacCode > 4095)
            dacCode = 4095;
        return (ushort)dacCode;
    }

    /// <summary>
    /// Calculates the allowable requested offset range at the connector input for the given frontend & path.
    /// </summary>
    public static void CalculateAllowableOffsetRangeV(ILogger logger, FrontendCalibration frontend, FrontendPathCalibration path, bool attenuator, double temperature, out double minOffsetV, out double maxOffsetV)
    {
        // Note: PGA input CM limit (pgaInputMaxDeviationFromBiasV) is enforced by the path calibration dpot never going below '4' (1562.5 ohms = around 1.9V to 3.1V input CM)

        var gainFactor = attenuator ? frontend.AttenuatorScale : 1.0;
        if (gainFactor <= 0)
        {
            logger.LogCritical("Invalid gain factor <= 0");
            minOffsetV = 0;
            maxOffsetV = 0;
            return;
        }

        var offsetDacLsbV = path.BufferInputVpp / path.TrimDacScale;
        if (offsetDacLsbV <= 0)
        {
            logger.LogCritical("Invalid offset DAC LSB <= 0");
            minOffsetV = 0;
            maxOffsetV = 0;
            return;
        }

        var dacZero = Frontend.GetTrimDacZero(temperature, path.TrimDacZeroM, path.TrimDacZeroC);
        var minOffsetFromDacV = (0 - dacZero) * offsetDacLsbV / gainFactor;
        var maxOffsetFromDacV = (4095 - dacZero) * offsetDacLsbV / gainFactor;

        minOffsetV = minOffsetFromDacV;
        maxOffsetV = maxOffsetFromDacV;

        if (minOffsetV > maxOffsetV)
        {
            logger.LogCritical("Invalid offset calculation, min offset > max offset");
            minOffsetV = 0;
            maxOffsetV = 0;
        }
    }

    /// <summary>
    /// Calculates if the requested voltage offset is supported with the given frontend & path.
    /// </summary>
    public static bool SupportsRequestedOffset(ILogger logger, FrontendCalibration frontend, FrontendPathCalibration path, bool mainAttenuator, double temperature, double requestedVoltOffset)
    {
        Frontend.CalculateAllowableOffsetRangeV(logger, frontend, path, mainAttenuator, temperature, out var minOffsetV, out var maxOffsetV);
        return requestedVoltOffset >= minOffsetV && requestedVoltOffset <= maxOffsetV;
    }

    /// <summary>
    /// Calculates the voltage peak-peak at the buffer input with the given path.
    /// </summary>
    public static double CalculateBufferInputVpp(int channelIndex, uint rate, FrontendPathCalibration path, double channelLoadScale)
    {
        return path.BufferInputVpp * channelLoadScale;
    }

    /// <summary>
    /// Calculates the voltage peak-peak at the BNC input with the given frontend & path.
    /// </summary>
    public static double CalculateConnectorInputVpp(int channelIndex, uint rate, FrontendCalibration frontend, FrontendPathCalibration path, double channelLoadScale, ThunderscopeTermination termination, bool attenuator, bool beta)
    {
        var scaleFactor = 1.0;
        if (attenuator)
            scaleFactor = frontend.AttenuatorScale;
        // For beta units, there is a scale factor for ThunderscopeTermination.FiftyOhm.
        if (beta && termination == ThunderscopeTermination.FiftyOhm)
            scaleFactor *= 5.0;

        return CalculateBufferInputVpp(channelIndex, rate, path, channelLoadScale) * scaleFactor;
    }

    public static double[] GetAllLoadScales(Calibration calibration, int[] channelsEnabled, uint sampleRateHz)
    {
        double[] channelLoadScales = [1.0, 1.0, 1.0, 1.0];

        // All channels disabled
        if (channelsEnabled.Length == 0)
        {
            return channelLoadScales;
        }

        // 3 channels enabled, so use 4 channel calibration
        if (channelsEnabled.Length == 3)
        {
            channelsEnabled = [0, 1, 2, 3];
        }

        // Channel configuration not found in calibration
        if (!calibration.Adc.LoadScale.Any(x => x.Channel.SequenceEqual(channelsEnabled)))
        {
            return channelLoadScales;
        }

        var loadScaleCalibration = calibration.Adc.LoadScale.First(x => x.Channel.SequenceEqual(channelsEnabled));

        // Sample rate not found in calibration
        if (!loadScaleCalibration.RateScale.Any(r => r.Rate == sampleRateHz))
        {
            return channelLoadScales;
        }
        var rateLoadScale = loadScaleCalibration.RateScale.First(r => r.Rate == sampleRateHz);

        // Iterate through and get all the valid load scales
        for (int i = 0; i <= 3; i++)
        {
            var channelPositionIndex = Array.IndexOf(loadScaleCalibration.Channel, i);
            // Disabled channel
            if (channelPositionIndex == -1)
                continue;
            channelLoadScales[i] = rateLoadScale.Scale[channelPositionIndex];
        }

        return channelLoadScales;
    }

    public static byte[] GetAdcBranchGain(Calibration calibration, int[] channelsEnabled, uint cachedSampleRateHz)
    {
        if (channelsEnabled.Length == 0)
        {
            return [0, 0, 0, 0, 0, 0, 0, 0];
        }
        else if (channelsEnabled.Length == 3)
        {
            channelsEnabled = [0, 1, 2, 3];
        }

        var branchGain = calibration.Adc.BranchGain.First(x => x.Channel.SequenceEqual(channelsEnabled));
        var rateBranchGain = branchGain.RateGain.First(x => x.Rate == cachedSampleRateHz);
        var adcBranchGain = new byte[8];
        adcBranchGain[0] = (byte)(rateBranchGain.Gain[0] & 0x7F);
        adcBranchGain[1] = (byte)(rateBranchGain.Gain[1] & 0x7F);
        adcBranchGain[2] = (byte)(rateBranchGain.Gain[2] & 0x7F);
        adcBranchGain[3] = (byte)(rateBranchGain.Gain[3] & 0x7F);
        adcBranchGain[4] = (byte)(rateBranchGain.Gain[4] & 0x7F);
        adcBranchGain[5] = (byte)(rateBranchGain.Gain[5] & 0x7F);
        adcBranchGain[6] = (byte)(rateBranchGain.Gain[6] & 0x7F);
        adcBranchGain[7] = (byte)(rateBranchGain.Gain[7] & 0x7F);
        Console.WriteLine($"Updating ADC branch gain with: {string.Join(", ", rateBranchGain.Gain.Select(g => g.ToString()))}");

        return adcBranchGain;
    }

    public static ThunderscopeChannelFrontend CalculateFrontend(ILogger logger, int channelIndex, ThunderscopeChannelFrontend channel, FrontendCalibration frontend, double channelLoadScale, uint sampleRateHz, double temperature, bool beta, out ThunderscopeChannelFrontendManualControl manualControl)
    {
        // Note: PGA input voltage should not go beyond +/-0.6V from 2.5V so that enforces a limit in some gain scenarios. 
        //       Datasheet says +/-0.6V. Testing shows up to +/-1.3V. Use datasheet specification.
        const double pgaInputMaxDeviationFromBiasV = 0.6;

        FrontendPathCalibration? selectedPath = null;
        bool attenuator = false;
        var maximumDesignRangeForTermination = channel.RequestedTermination switch
        {
            ThunderscopeTermination.OneMegaohm => 40.0,
            ThunderscopeTermination.FiftyOhm => 5.0 * (2 * 1.41 * 1.1),     // 5Vrms with 10% headroom
            _ => throw new NotImplementedException()
        };
        var minimumDesignRange = 0.008;
        var vppTooLarge = channel.RequestedVoltFullScale > maximumDesignRangeForTermination;
        var vppTooSmall = channel.RequestedVoltFullScale < minimumDesignRange;
        var runRangeSearch = !vppTooSmall && !vppTooLarge;
        channel.ActualTermination = channel.RequestedTermination;       // True in most cases, there is below that can change it

        while (runRangeSearch)
        {
            // Scan through the frontend gain configurations until the actual volt range would exceed the requested voltage range and ensure requested offset is reachable with the chosen path.
            // Pga with no attenuator
            foreach (var path in frontend.Path)
            {
                var potentialVpp = Frontend.CalculateConnectorInputVpp(channelIndex, sampleRateHz, frontend, path, channelLoadScale, channel.RequestedTermination, attenuator, beta);
                //var potentialVpp = CalculateConnectorInputVpp(channelIndex, path, channel.RequestedTermination, attenuator, frontend.AttenuatorScale);
                if (potentialVpp > channel.RequestedVoltFullScale)
                {
                    if (!Frontend.SupportsRequestedOffset(logger, frontend, path, attenuator, temperature, channel.RequestedVoltOffset))
                    {
                        continue;
                    }

                    selectedPath = path;
                    break;
                }
            }
            if (selectedPath is not null)
                break;
            attenuator = true;
            // Pga with attenuator
            foreach (var path in frontend.Path)
            {
                var potentialVpp = Frontend.CalculateConnectorInputVpp(channelIndex, sampleRateHz, frontend, path, channelLoadScale, channel.RequestedTermination, attenuator, beta);
                if (potentialVpp > channel.RequestedVoltFullScale)
                {
                    if (!Frontend.SupportsRequestedOffset(logger, frontend, path, attenuator, temperature, channel.RequestedVoltOffset))
                    {
                        continue;
                    }

                    selectedPath = path;
                    break;
                }
            }
            break;
        }

        if (selectedPath is null)
        {
            logger.LogWarning("No valid frontend configuration found, using nearest");
            switch (channel.RequestedTermination)
            {
                case ThunderscopeTermination.OneMegaohm:
                    if (vppTooLarge)
                    {
                        selectedPath = frontend.Path.Last();
                        attenuator = true;
                        logger.LogWarning($"Requested range too large");
                    }
                    else if (vppTooSmall)
                    {
                        selectedPath = frontend.Path.First();
                        attenuator = false;
                        logger.LogWarning("Requested range too small");
                    }
                    else
                    {
                        selectedPath = frontend.Path.Last();
                        attenuator = true;
                        if (!Frontend.SupportsRequestedOffset(logger, frontend, selectedPath, attenuator, temperature, channel.RequestedVoltOffset))
                        {
                            logger.LogWarning("Requested offset too large");
                        }
                    }
                    break;
                case ThunderscopeTermination.FiftyOhm:
                    if (vppTooLarge)
                    {
                        // Switch off the 50R termination
                        channel.ActualTermination = ThunderscopeTermination.OneMegaohm;
                        logger.LogWarning("Termination changed to 1M");

                        selectedPath = frontend.Path.Last();
                        attenuator = true;
                        logger.LogWarning("Requested range too large");
                    }
                    else if (vppTooSmall)
                    {
                        selectedPath = frontend.Path.First();
                        attenuator = false;
                        logger.LogWarning("Requested range too small");
                    }
                    else
                    {
                        // Switch off the 50R termination
                        channel.ActualTermination = ThunderscopeTermination.OneMegaohm;
                        logger.LogWarning("Termination changed to 1M");

                        selectedPath = frontend.Path.Last();
                        attenuator = true;
                        if (!Frontend.SupportsRequestedOffset(logger, frontend, selectedPath, attenuator, temperature, channel.RequestedVoltOffset))
                        {
                            logger.LogWarning("Requested offset too large");
                        }
                    }
                    break;
                default:
                    throw new NotImplementedException();
            }
        }


        // Note: attenuator is the only source of gainFactor change. Probe scaling should be accounted for at the UI level.
        double gainFactor = 1.0;
        if (attenuator)
            gainFactor = frontend.AttenuatorScale;
        // Note: if desired offset is beyond acceptable range for PGA input voltage limits, clamp it.
        // -1 to make the SCPI API match most scope vendors, i.e. if input signal has 100mV offset, send CHAN1:OFFS 0.1 to cancel it out.
        var dacZero = Frontend.GetTrimDacZero(temperature, selectedPath.TrimDacZeroM, selectedPath.TrimDacZeroC);
        var dacLsbV = selectedPath.BufferInputVpp / selectedPath.TrimDacScale;
        var dacOffset = (int)((channel.RequestedVoltOffset * gainFactor) / dacLsbV);
        var dacValue = dacZero + dacOffset;

        // Note: last resort clamping of DAC value.
        if (dacValue < 0)
            dacValue = 0;
        if (dacValue > 4095)
            dacValue = 4095;

        channel.ActualVoltFullScale = Frontend.CalculateConnectorInputVpp(channelIndex, sampleRateHz, frontend, selectedPath, channelLoadScale, channel.RequestedTermination, attenuator, beta);
        channel.ActualVoltOffset = (dacOffset * dacLsbV) / gainFactor;
        Frontend.CalculateAllowableOffsetRangeV(logger, frontend, selectedPath, attenuator, temperature, out var minOffsetV, out var maxOffsetV);
        channel.MinVoltOffset = minOffsetV;
        channel.MaxVoltOffset = maxOffsetV;

        manualControl = new ThunderscopeChannelFrontendManualControl()
        {
            Coupling = channel.Coupling,
            Termination = channel.ActualTermination,
            Attenuator = attenuator ? (byte)1 : (byte)0,
            DAC = (ushort)dacValue,
            DPOT = selectedPath.TrimDPot,
            PgaLadderAttenuation = selectedPath.PgaLadder,
            PgaFilter = channel.Bandwidth,
            PgaHighGain = (selectedPath.PgaPreampGain == PgaPreampGain.High) ? (byte)1 : (byte)0
        };

        return channel;
    }
}
