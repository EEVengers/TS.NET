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
                trigger.Process(situation.Input.Span.Slice((int)(i * situation.ChunkSize), (int)situation.ChunkSize), ref edgeTriggerResults);
            }

            for (int i = 0; i < situation.ExpectedWindowEndIndices.Length; i++)
            {
                Assert.Equal(situation.ExpectedWindowEndIndices.Span[i], edgeTriggerResults.CaptureEndIndices[i]);
            }
        }
    }
}
