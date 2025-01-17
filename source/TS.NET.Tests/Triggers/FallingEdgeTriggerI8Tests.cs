using System;
using Xunit;

namespace TS.NET.Tests
{
    public class FallingEdgeTriggerI8Tests
    {
        [Fact]
        public void SituationA()
        {
            var situation = FallingEdgeTriggerSituations.SituationA();
            RunSituation(situation);
        }

        [Fact]
        public void SituationB()
        {
            var situation = FallingEdgeTriggerSituations.SituationB();
            RunSituation(situation);
        }

        [Fact]
        public void SituationC()
        {
            var situation = FallingEdgeTriggerSituations.SituationC();
            RunSituation(situation);
        }

        [Fact]
        public void SituationD()
        {
            var situation = FallingEdgeTriggerSituations.SituationD();
            RunSituation(situation);
        }

        [Fact]
        public void SituationE()
        {
            var situation = FallingEdgeTriggerSituations.SituationE();
            RunSituation(situation);
        }

        private static void RunSituation(EdgeTriggerSituation situation)
        {
            FallingEdgeTriggerI8 trigger = new(situation.Parameters);
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