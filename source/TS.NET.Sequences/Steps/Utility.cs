namespace TS.NET.Sequences;

public static class Utility
{
    [Obsolete]
    public static FrontendPathCalibration GetChannelPathCalibration(int channelIndex, int pathIndex, CommonVariables variables)
    {
        return variables.Calibration.Frontend[channelIndex].Path[pathIndex];
    }

    public static FrontendPathCalibration GetChannelPathCalibration(int channelIndex, PgaPreampGain pgaPreamp, int pgaLadder, CommonVariables variables)
    {
        return variables.Calibration.Frontend[channelIndex].Path.Where(p => p.PgaPreampGain == pgaPreamp && p.PgaLadder == pgaLadder).First();
    }

    public static ChannelPathData GetChannelPathData(int channelIndex, PgaPreampGain pgaPreamp, byte pgaLadder, CalibrationVariables variables)
    {
        var pathConfigs = channelIndex switch
        {
            0 => variables.Channel1PathConfigs,
            1 => variables.Channel2PathConfigs,
            2 => variables.Channel3PathConfigs,
            3 => variables.Channel4PathConfigs,
            _ => throw new NotImplementedException()
        };
        return pathConfigs.Where(p => p.PgaPreampGain == pgaPreamp && p.PgaLadder == pgaLadder).First();
    }

    public static bool TryTrimDacBinarySearch(
        int channelIndex,
        FrontendPathCalibration path,
        double targetMin, double targetMax,
        int trimSettlingTimeMs,
        CancellationToken cancellationToken,
        out ushort dac, out double adc)
    {
        int low = 0;
        int high = 4095;
        while (low <= high)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int mid = low + (high - low) / 2;
            Instruments.Instance.SetThunderscopeCalManual50R(channelIndex, (ushort)mid, path.TrimDPot, path.PgaPreampGain, path.PgaLadder, trimSettlingTimeMs);
            var average = Instruments.Instance.GetThunderscopeAverage(channelIndex, sampleCount: 10_000_000);
            if (average >= targetMin && average <= targetMax)
            {
                dac = (ushort)mid;
                adc = average;
                return true;
            }
            if (average > targetMax)
            {
                low = mid + 1;
            }
            else if (average < targetMin)
            {
                high = mid - 1;
            }
        }
        dac = 0;
        adc = 0;
        return false;
    }

    public static double GetAndCheckSigGenZero(int channelIndex, ChannelPathData path, BenchCalibrationVariables variables, CancellationToken cancellationToken)
    {
        double zeroValue = variables.SigGenZero;
        double average = 0;
        for (int i = 0; i < 500; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var high = path.Target8bAdcCountPerDacLsb * 2;
            var low = path.Target8bAdcCountPerDacLsb * -2;
            SigGens.Instance.SetSdgChannel([channelIndex]);
            SigGens.Instance.SetSdgParameterOffset(channelIndex, zeroValue);
            Thread.Sleep(100);
            average = Instruments.Instance.GetThunderscopeAverage(channelIndex, sampleCount: 10_000_000);
            if (average >= low && average <= high)
            {
                break;
            }
            if (average > high)
                zeroValue -= 0.0001;
            else
                zeroValue += 0.0001;
        }

        //Thread.Sleep(100);
        //// Check average is still good
        //var foundAverage = Instruments.Instance.GetThunderscopeAverage(channelIndex);
        //if (foundAverage > 15 || foundAverage < -15)
        //{
        //    Instruments.Instance.SetSdgOffset(channelIndex, 0);
        //    throw new TestbenchException($"Average check failed ({foundAverage})");
        //}

        variables.SigGenZero = zeroValue;
        return zeroValue;
    }

    //public static double FindVpp(int channelIndex, ChannelPathData pathConfig, double zeroValue, CancellationToken cancellationToken)
    //{
    //    for (double amplitude = pathConfig.SigGenAmplitudeStart; amplitude <= 5; amplitude += pathConfig.SigGenAmplitudeStep)
    //    {
    //        cancellationToken.ThrowIfCancellationRequested();
    //        SigGens.Instance.SetSdgChannel([channelIndex]);

    //        SigGens.Instance.SetSdgParameterOffset(channelIndex, zeroValue + amplitude);
    //        Thread.Sleep(100);
    //        var average = Instruments.Instance.GetThunderscopeAverage(channelIndex, sampleCount: 10_000_000);
    //        var max = average;

    //        cancellationToken.ThrowIfCancellationRequested();
    //        SigGens.Instance.SetSdgParameterOffset(channelIndex, zeroValue - amplitude);
    //        Thread.Sleep(100);
    //        average = Instruments.Instance.GetThunderscopeAverage(channelIndex, sampleCount: 10_000_000);
    //        var min = average;

    //        var range = max - min;

    //        if (range >= 240)
    //        {
    //            SigGens.Instance.SetSdgParameterOffset(channelIndex, 0);
    //            throw new TestbenchException($"Could not converge, range: {range}");
    //        }
    //        if (range > 200 && range < 240)
    //        {
    //            var ratio = 256.0 / range;
    //            var vpp = amplitude * ratio * 2;
    //            SigGens.Instance.SetSdgParameterOffset(channelIndex, 0);
    //            return vpp;
    //        }
    //    }

    //    SigGens.Instance.SetSdgParameterOffset(channelIndex, 0);
    //    throw new TestbenchException("Could not converge, amplitude: 10");
    //}

    public static double FindVpp(int channelIndex, ChannelPathData pathConfig, double zeroValue, double sampleRateHz, CancellationToken cancellationToken)
    {
        const uint frequencyHz = 1000;

        for (double amplitude = pathConfig.SigGenAmplitudeStart; amplitude <= 5; amplitude += pathConfig.SigGenAmplitudeStep)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SigGens.Instance.SetSdgChannel([channelIndex]);
            SigGens.Instance.SetSdgSine(channelIndex);
            SigGens.Instance.SetSdgParameterAmplitude(channelIndex, amplitude);
            SigGens.Instance.SetSdgParameterFrequency(channelIndex, frequencyHz);
            SigGens.Instance.SetSdgParameterOffset(channelIndex, zeroValue);
            Thread.Sleep(10);

            var adcPP = Instruments.Instance.GetThunderscopeAdcPeakPeakAtFrequencyLsq(channelIndex, frequencyHz, sampleRateHz);

            if (adcPP > 243)
            {
                throw new TestbenchException($"Could not converge, ADC reading too large");
            }
            if (adcPP >= 218 && adcPP <= 243)     // 218 to 243 = 85% to 95%
            {
                var ratio = 256.0 / adcPP;
                var vpp = amplitude * ratio;
                return vpp;
            }
        }
        throw new TestbenchException("Could not converge, signal generator amplitude too large");
    }
}
