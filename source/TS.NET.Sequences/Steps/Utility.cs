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

    public static double GetAndCheckSigGenZero(int channelIndex, ChannelPathConfig path, BenchCalibrationVariables variables, CancellationToken cancellationToken)
    {
        double zeroValue = variables.SigGenZero;
        double average = 0;
        for (int i = 0; i < 500; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var high = path.TargetDPotResolution * 2;
            var low = path.TargetDPotResolution * -2;
            Instruments.Instance.SetSdgOffset(channelIndex, zeroValue);
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
        for (double amplitude = pathConfig.SigGenAmplitudeStart; amplitude <= 10; amplitude += pathConfig.SigGenAmplitudeStep)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Instruments.Instance.SetSdgOffset(channelIndex, zeroValue + amplitude);
            Thread.Sleep(100);
            var average = Instruments.Instance.GetThunderscopeAverage(channelIndex);
            var max = average;

            cancellationToken.ThrowIfCancellationRequested();
            Instruments.Instance.SetSdgOffset(channelIndex, zeroValue - amplitude);
            Thread.Sleep(100);
            average = Instruments.Instance.GetThunderscopeAverage(channelIndex);
            var min = average;

            var range = max - min;

            if (range >= 240)
            {
                Instruments.Instance.SetSdgOffset(channelIndex, 0);
                throw new TestbenchException($"Could not converge, range: {range}");
            }
            if (range > 200 && range < 240)
            {
                var ratio = 256.0 / range;
                var vpp = amplitude * ratio * 2;
                Instruments.Instance.SetSdgOffset(channelIndex, 0);
                return vpp;
            }
        }

        Instruments.Instance.SetSdgOffset(channelIndex, 0);
        throw new TestbenchException("Could not converge, amplitude: 10");
    }
}
