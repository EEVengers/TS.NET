using System;
using Xunit;

namespace TS.NET.Tests
{
    public class RisingEdgeTriggerI8Tests
    {
        [Fact]
        public void SituationA()
        {
            var situation = RisingEdgeTriggerSituations.SituationA();
            RunSituation(situation);
        }

        [Fact]
        public void SituationB()
        {
            var situation = RisingEdgeTriggerSituations.SituationB();
            RunSituation(situation);
        }

        [Fact]
        public void SituationC()
        {
            var situation = RisingEdgeTriggerSituations.SituationC();
            RunSituation(situation);
        }

        [Fact]
        public void SituationD()
        {
            var situation = RisingEdgeTriggerSituations.SituationD();
            RunSituation(situation);
        }

        [Fact]
        public void SituationE()
        {
            var situation = RisingEdgeTriggerSituations.SituationE();
            RunSituation(situation);
        }

        private static void RunSituation(EdgeTriggerSituation situation)
        {
            RisingEdgeTriggerI8 trigger = new(situation.Parameters);
            trigger.SetHorizontal(situation.WindowWidth, situation.WindowTriggerPosition, situation.AdditionalHoldoff);

            Span<int> captureEndIndices = new int[10000];
            var currentWindowEndIndices = captureEndIndices;

            for (int i = 0; i < situation.ChunkCount; i++)
            {
                trigger.Process(situation.Input.Span.Slice((i * situation.ChunkSize), situation.ChunkSize), currentWindowEndIndices, out int windowEndCount);
                currentWindowEndIndices = currentWindowEndIndices.Slice(windowEndCount);
            }

            for (int i = 0; i < situation.ExpectedWindowEndIndices.Length; i++)
            {
                Assert.Equal(situation.ExpectedWindowEndIndices.Span[i], captureEndIndices[i]);
            }
        }
    }
}