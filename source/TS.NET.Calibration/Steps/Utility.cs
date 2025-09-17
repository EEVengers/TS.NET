namespace TS.NET.Calibration;

public static class Utility
{
    public static ThunderscopeChannelPathCalibration GetChannelPathCalibration(int channelIndex, int pathIndex)
    {
        var calibration = channelIndex switch
        {
            0 => Variables.Instance.Calibration.Channel1,
            1 => Variables.Instance.Calibration.Channel2,
            2 => Variables.Instance.Calibration.Channel3,
            3 => Variables.Instance.Calibration.Channel4,
            _ => throw new NotImplementedException()
        };
        return calibration.Paths[pathIndex];
    }

    public static ChannelPathConfig GetChannelPathConfig(int channelIndex, int pathIndex)
    {
        var pathConfigs = channelIndex switch
        {
            0 => Variables.Instance.Channel1PathConfigs,
            1 => Variables.Instance.Channel1PathConfigs,
            2 => Variables.Instance.Channel1PathConfigs,
            3 => Variables.Instance.Channel1PathConfigs,
            _ => throw new NotImplementedException()
        };
        return pathConfigs[pathIndex];
    }

    public static double GetAndCheckSigGenZero(int channelIndex, ChannelPathConfig path, CancellationToken cancellationToken)
    {
        double zeroValue = Variables.Instance.SigGenZero;
        double average = 0;
        for (int i = 0; i < 500; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var high = path.TargetDPotResolution * 2;
            var low = path.TargetDPotResolution * -2;
            Instruments.Instance.SetSdgDcOffset(channelIndex, zeroValue);
            Thread.Sleep(250);
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

        Thread.Sleep(250);
        // Check average is still good
        var foundAverage = Instruments.Instance.GetThunderscopeAverage(channelIndex);
        if (foundAverage > 15 || foundAverage < -15)
        {
            Instruments.Instance.SetSdgDcOffset(channelIndex, 0);
            throw new CalibrationException($"Average check failed ({foundAverage})");
        }

        Variables.Instance.SigGenZero = zeroValue;
        return zeroValue;
    }

    public static double FindVpp(int channelIndex, ChannelPathConfig pathConfig, double zeroValue, CancellationToken cancellationToken)
    {
        for (double amplitude = pathConfig.SigGenAmplitudeStart; amplitude <= 10; amplitude += pathConfig.SigGenAmplitudeStep)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Instruments.Instance.SetSdgDcOffset(channelIndex, zeroValue + amplitude);
            Thread.Sleep(250);
            var average = Instruments.Instance.GetThunderscopeAverage(channelIndex);
            var max = average;

            cancellationToken.ThrowIfCancellationRequested();
            Instruments.Instance.SetSdgDcOffset(channelIndex, zeroValue - amplitude);
            Thread.Sleep(250);
            average = Instruments.Instance.GetThunderscopeAverage(channelIndex);
            var min = average;

            var range = max - min;

            if (range >= 240)
            {
                Instruments.Instance.SetSdgDcOffset(channelIndex, 0);
                throw new CalibrationException($"Could not converge, range: {range}");
            }
            if (range > 200 && range < 240)
            {
                var ratio = 255.0 / range;
                var vpp = amplitude * ratio * 2;
                Instruments.Instance.SetSdgDcOffset(channelIndex, 0);
                return vpp;
            }
        }

        Instruments.Instance.SetSdgDcOffset(channelIndex, 0);
        throw new CalibrationException("Could not converge, amplitude: 10");
    }
}
