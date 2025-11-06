namespace TS.NET
{
    internal static class TriggerUtility
    {
        public static int HysteresisValue(AdcResolution adcResolution, double hysteresisPercent)
        {
            var adcRange = adcResolution switch
            {
                AdcResolution.EightBit => 256,
                AdcResolution.TwelveBit => 4096,
                _ => throw new NotImplementedException()
            };

            if (hysteresisPercent < 0)
                hysteresisPercent = 0;
            if (hysteresisPercent > 50)
                hysteresisPercent = 50;

            int hysteresisValue = (int)((hysteresisPercent / 100.0) * adcRange);

            return hysteresisValue;
        }

        public static int LevelValue(AdcResolution adcResolution, double levelV, double triggerChannelVpp)
        {
            var adcRange = adcResolution switch
            {
                AdcResolution.EightBit => 256,
                //AdcResolution.TwelveBit => 4096,
                AdcResolution.TwelveBit => 65536,
                _ => throw new NotImplementedException()
            };

            var channelRangeRatio = levelV / triggerChannelVpp;
            int levelValue = (int)Math.Clamp(Math.Round(channelRangeRatio * adcRange), AdcMin(adcResolution), AdcMax(adcResolution));

            return levelValue;
        }

        public static int AdcMin(AdcResolution adcResolution)
        {
            var adcMin = adcResolution switch
            {
                AdcResolution.EightBit => sbyte.MinValue,
                //AdcResolution.TwelveBit => -2048,
                AdcResolution.TwelveBit => -32768,
                _ => throw new NotImplementedException()
            };

            return adcMin;
        }

        public static int AdcMax(AdcResolution adcResolution)
        {
            var adcMax = adcResolution switch
            {
                AdcResolution.EightBit => sbyte.MaxValue,
                //AdcResolution.TwelveBit => 2047,
                AdcResolution.TwelveBit => 32767,
                _ => throw new NotImplementedException()
            };
            return adcMax;
        }
    }
}
