namespace TS.NET.Sequences;

public static class ReportStringUtility
{
    // Note: TimeSpanUtility.HumanDuration

    public static string LadderIndexToDbToHumanReadable(int ladder)
    {
        return ladder switch
        {
            0 => "0 dB",
            1 => "-2 dB",
            2 => "-4 dB",
            3 => "-6 dB",
            4 => "-8 dB",
            5 => "-10 dB",
            6 => "-12 dB",
            7 => "-14 dB",
            8 => "-16 dB",
            9 => "-18 dB",
            10 => "-20 dB",
            _ => throw new NotImplementedException(),
        };
    }

    public static string SampleRateHzToHumanReadable(uint sampleRateHz)
    {
        return sampleRateHz switch
        {
            >= 1_000_000_000 => $"{sampleRateHz / 1_000_000_000} GSPS",
            >= 1_000_000 => $"{sampleRateHz / 1_000_000} MSPS",
            >= 1_000 => $"{sampleRateHz / 1_000} kSPS",
            _ => $"{sampleRateHz} SPS",
        };
    }

    public static string AdcResolutionToHumanReadable(AdcResolution resolution)
    {
        return resolution switch
        {
            AdcResolution.EightBit => "8 bits",
            AdcResolution.TwelveBit => "12 bits",
            _ => throw new NotImplementedException(),
        };
    }
}