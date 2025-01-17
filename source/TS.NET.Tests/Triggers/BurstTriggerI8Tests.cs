using System;
using Xunit;

namespace TS.NET.Tests
{
    public class BurstTriggerI8Tests
    {
        [Fact]
        public void SituationA()
        {
            var situation = BurstTriggerSituations.SituationA();
            RunSituation(situation);
        }

        private static void RunSituation(BurstTriggerSituation situation)
        {
            BurstTriggerI8 trigger = new(situation.Parameters);
            trigger.SetHorizontal(situation.WindowWidth, situation.WindowTriggerPosition, situation.AdditionalHoldoff);

            Span<uint> captureEndIndices = new uint[10000];
            var currentWindowEndIndices = captureEndIndices;

            for (int i = 0; i < situation.ChunkCount; i++)
            {
                trigger.Process(situation.Input.Span.Slice((int)(i * situation.ChunkSize), (int)situation.ChunkSize), currentWindowEndIndices, out uint windowEndCount);
                currentWindowEndIndices = currentWindowEndIndices.Slice((int)windowEndCount);
            }

            for (int i = 0; i < situation.ExpectedWindowEndIndices.Length; i++)
            {
                Assert.Equal(situation.ExpectedWindowEndIndices.Span[i], captureEndIndices[i]);
            }
        }
    }
}
