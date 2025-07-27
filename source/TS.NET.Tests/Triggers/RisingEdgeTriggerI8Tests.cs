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
            var trigger = new RisingEdgeTriggerI8(situation.Parameters);
            var edgeTriggerResults = new EdgeTriggerResults()
            {
                ArmIndices = new int[1000],
                TriggerIndices = new int[1000],
                CaptureEndIndices = new int[1000]
            };
            trigger.SetHorizontal(situation.WindowWidth, situation.WindowTriggerPosition, situation.AdditionalHoldoff);

            if (situation.ChunkCount > 1)
                throw new NotImplementedException();

            for (int i = 0; i < situation.ChunkCount; i++)
            {
                trigger.Process(situation.Input.Span.Slice((i * situation.ChunkSize), situation.ChunkSize), ref edgeTriggerResults);
            }

            for (int i = 0; i < situation.ExpectedWindowEndIndices.Length; i++)
            {
                Assert.Equal(situation.ExpectedWindowEndIndices.Span[i], edgeTriggerResults.CaptureEndIndices[i]);
            }
        }
    }
}