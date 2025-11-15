namespace TS.NET.Sequences;

public static class Utility
{
    public static ThunderscopeChannelPathCalibration GetChannelPathCalibration(int channelIndex, int pathIndex, CommonVariables variables)
    {
        var calibration = channelIndex switch
        {
            0 => variables.Calibration.Channel1,
            1 => variables.Calibration.Channel2,
            2 => variables.Calibration.Channel3,
            3 => variables.Calibration.Channel4,
            _ => throw new NotImplementedException()
        };
        return calibration.Paths[pathIndex];
    }

    public static ThunderscopeChannelPathCalibration GetChannelPathCalibration(int channelIndex, PgaPreampGain pgaPreamp, int pgaLadder, CommonVariables variables)
    {
        var calibration = channelIndex switch
        {
            0 => variables.Calibration.Channel1,
            1 => variables.Calibration.Channel2,
            2 => variables.Calibration.Channel3,
            3 => variables.Calibration.Channel4,
            _ => throw new NotImplementedException()
        };
        return calibration.Paths.Where(p => p.PgaPreampGain == pgaPreamp && p.PgaLadderAttenuator == pgaLadder).First();
    }

    public static ChannelPathConfig GetChannelPathConfig(int channelIndex, int pathIndex, CalibrationVariables variables)
    {
        var pathConfigs = channelIndex switch
        {
            0 => variables.Channel1PathConfigs,
            1 => variables.Channel1PathConfigs,
            2 => variables.Channel1PathConfigs,
            3 => variables.Channel1PathConfigs,
            _ => throw new NotImplementedException()
        };
        return pathConfigs[pathIndex];
    }

    public static ChannelPathConfig GetChannelPathConfig(int channelIndex, PgaPreampGain pgaPreamp, int pgaLadder, CalibrationVariables variables)
    {
        var pathConfigs = channelIndex switch
        {
            0 => variables.Channel1PathConfigs,
            1 => variables.Channel1PathConfigs,
            2 => variables.Channel1PathConfigs,
            3 => variables.Channel1PathConfigs,
            _ => throw new NotImplementedException()
        };
        return pathConfigs.Where(p => p.PgaPreampGain == pgaPreamp && p.PgaLadderAttenuator == pgaLadder).First();
    }

    public static bool TryTrimDacBinarySearch(
        int channelIndex,
        ThunderscopeChannelPathCalibration pathCalibration,
        double targetMin, double targetMax,
        int frontEndSettlingTimeMs,
        CancellationToken cancellationToken,
        out ushort dac, out double adc)
    {
        int low = 0;
        int high = 4095;
        while (low <= high)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int mid = low + (high - low) / 2;
            Instruments.Instance.SetThunderscopeCalManual50R(channelIndex, (ushort)mid, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, frontEndSettlingTimeMs);
            var average = Instruments.Instance.GetThunderscopeAverage(channelIndex);
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

    public static double GetAndCheckSigGenZero(int channelIndex, ChannelPathConfig path, BenchCalibrationVariables variables, CancellationToken cancellationToken)
    {
        double zeroValue = variables.SigGenZero;
        double average = 0;
        for (int i = 0; i < 500; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var high = path.TargetDPotResolution * 2;
            var low = path.TargetDPotResolution * -2;
            Instruments.Instance.SetSdgParameterOffset(channelIndex, zeroValue);
            Thread.Sleep(100);
            average = Instruments.Instance.GetThunderscopeAverage(channelIndex);
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

    public static double FindVpp(int channelIndex, ChannelPathConfig pathConfig, double zeroValue, CancellationToken cancellationToken)
    {
        for (double amplitude = pathConfig.SigGenAmplitudeStart; amplitude <= 5; amplitude += pathConfig.SigGenAmplitudeStep)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Instruments.Instance.SetSdgParameterOffset(channelIndex, zeroValue + amplitude);
            Thread.Sleep(100);
            var average = Instruments.Instance.GetThunderscopeAverage(channelIndex);
            var max = average;

            cancellationToken.ThrowIfCancellationRequested();
            Instruments.Instance.SetSdgParameterOffset(channelIndex, zeroValue - amplitude);
            Thread.Sleep(100);
            average = Instruments.Instance.GetThunderscopeAverage(channelIndex);
            var min = average;

            var range = max - min;

            if (range >= 240)
            {
                Instruments.Instance.SetSdgParameterOffset(channelIndex, 0);
                throw new TestbenchException($"Could not converge, range: {range}");
            }
            if (range > 200 && range < 240)
            {
                var ratio = 256.0 / range;
                var vpp = amplitude * ratio * 2;
                Instruments.Instance.SetSdgParameterOffset(channelIndex, 0);
                return vpp;
            }
        }

        Instruments.Instance.SetSdgParameterOffset(channelIndex, 0);
        throw new TestbenchException("Could not converge, amplitude: 10");
    }
}
