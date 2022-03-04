using System;
using Xunit;

namespace TS.NET.Tests
{
    public class RisingEdgeTriggerTests
    {
        [Fact]
        public void SituationA_NonSimd()
        {
            var data = RisingEdgeTriggerSituations.SituationA();
            RisingEdgeTrigger trigger = new(data.TriggerLevel, data.ArmLevel, data.HoldoffSamples);
            Span<ulong> actualTriggers = new ulong[data.ExpectedTriggers.Length];
            trigger.Process(data.Input.Span, actualTriggers);

            for (int i = 0; i < actualTriggers.Length; i++)
            {
                Assert.Equal(data.ExpectedTriggers.Span[i], actualTriggers[i]);
            }
        }

        [Fact]
        public void SituationA_Simd()
        {
            var data = RisingEdgeTriggerSituations.SituationA();
            RisingEdgeTrigger trigger = new(data.TriggerLevel, data.ArmLevel, data.HoldoffSamples);
            Span<ulong> actualTriggers = new ulong[data.ExpectedTriggers.Length];
            trigger.ProcessSimd(data.Input.Span, actualTriggers);

            for (int i = 0; i < actualTriggers.Length; i++)
            {
                Assert.Equal(data.ExpectedTriggers.Span[i], actualTriggers[i]);
            }
        }
    }
}