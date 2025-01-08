using System;
using Xunit;

namespace TS.NET.Tests
{
    public class AnyEdgeTriggerI8Tests
    {
        [Fact]
        public void SituationA_Rising()
        {
            var situation = RisingEdgeTriggerSituations.SituationA();
            RunSituation(situation);
        }

        [Fact]
        public void SituationB_Rising()
        {
            var situation = RisingEdgeTriggerSituations.SituationB();
            RunSituation(situation);
        }

        [Fact]
        public void SituationC_Rising()
        {
            var situation = RisingEdgeTriggerSituations.SituationC();
            RunSituation(situation);
        }

        [Fact]
        public void SituationD_Rising()
        {
            var situation = RisingEdgeTriggerSituations.SituationD();
            RunSituation(situation);
        }

        [Fact]
        public void SituationE_Rising()
        {
            var situation = RisingEdgeTriggerSituations.SituationE();
            RunSituation(situation);
        }

        [Fact]
        public void SituationA_Falling()
        {
            var situation = RisingEdgeTriggerSituations.SituationA();
            RunSituation(situation);
        }

        [Fact]
        public void SituationB_Falling()
        {
            var situation = RisingEdgeTriggerSituations.SituationB();
            RunSituation(situation);
        }

        [Fact]
        public void SituationC_Falling()
        {
            var situation = RisingEdgeTriggerSituations.SituationC();
            RunSituation(situation);
        }

        [Fact]
        public void SituationD_Falling()
        {
            var situation = RisingEdgeTriggerSituations.SituationD();
            RunSituation(situation);
        }

        [Fact]
        public void SituationE_Falling()
        {
            var situation = RisingEdgeTriggerSituations.SituationE();
            RunSituation(situation);
        }

        private static void RunSituation(EdgeTriggerSituation situation)
        {
            RisingEdgeTriggerI8 trigger = new(new EdgeTriggerParameters() { Level = situation.TriggerLevel, Hysteresis = situation.TriggerHysteresis, Direction = EdgeDirection.Any });
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
