using System;

namespace TS.NET.Sequencer
{
    public static class TimeSpanUtility
    {
        public static string HumanDuration(TimeSpan? duration)
        {
            if (duration == null)
                return "-";
            string format;
            if (duration?.Hours > 0)
                format = @"h\h\ m\m\ s\s";
            else if (duration?.Minutes > 0)
                format = @"m\m\ s\s";
            else if (duration?.Seconds > 10)
                format = @"s\s";
            else
                format = @"s\.fff\s";
            return ((TimeSpan)duration!).ToString(format);
        }
    }
}
