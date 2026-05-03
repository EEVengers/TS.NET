namespace TS.NET
{
    public static class TriggerUtility
    {
        public static int HysteresisValue(AdcResolution adcResolution, double hysteresisPercent)
        {
            if (hysteresisPercent < 0)
                hysteresisPercent = 0;
            if (hysteresisPercent > 50)
                hysteresisPercent = 50;

            int hysteresisValue = (int)((hysteresisPercent / 100.0) * AdcRange(adcResolution));

            return hysteresisValue;
        }

        public static int LevelValue(AdcResolution adcResolution, double levelV, double triggerChannelVpp)
        {
            var channelRangeRatio = levelV / triggerChannelVpp;
            int levelValue = (int)Math.Clamp(Math.Round(channelRangeRatio * AdcRange(adcResolution)), AdcMin(adcResolution), AdcMax(adcResolution));

            return levelValue;
        }

        public static int AdcRange(AdcResolution adcResolution)
        {
            var adcRange = adcResolution switch
            {
                AdcResolution.EightBit => 256,
                AdcResolution.TwelveBit => 4096,        // Format12BitLSB
                //AdcResolution.TwelveBit => 65536,       // Format12BitMSB
                _ => throw new NotImplementedException()
            };
            return adcRange;
        }

        public static int AdcMin(AdcResolution adcResolution)
        {
            var adcMin = adcResolution switch
            {
                AdcResolution.EightBit => sbyte.MinValue,
                AdcResolution.TwelveBit => -2048,       // Format12BitLSB
                //AdcResolution.TwelveBit => -32768,      // Format12BitMSB
                _ => throw new NotImplementedException()
            };

            return adcMin;
        }

        public static int AdcMax(AdcResolution adcResolution)
        {
            var adcMax = adcResolution switch
            {
                AdcResolution.EightBit => sbyte.MaxValue,
                AdcResolution.TwelveBit => 2047,        // Format12BitLSB
                //AdcResolution.TwelveBit => 32767,       // Format12BitMSB
                _ => throw new NotImplementedException()
            };
            return adcMax;
        }
    }
}
