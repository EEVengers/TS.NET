using Xunit;

namespace TS.NET.Tests
{
    public class PgaGainTests
    {
        [Fact]
        public void Test()
        {
            Thunderscope.CalculateAfeGainConfiguration(0.5,  out byte pgaConfiguration, out bool afeAttenuatorEnabled, out double actualVoltFullScale);
        }
    }
}
