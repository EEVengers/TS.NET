using System;
using Xunit;

namespace TS.NET.Tests;

public class BurstTriggerI8Tests
{
    [Fact]
    public void SituationA()
    {
        var situation = BurstTriggerSituations.SituationA();
        RunSituation(situation);
    }

    [Fact]
    public void SituationB()
    {
        var situation = BurstTriggerSituations.SituationB();
        RunSituation(situation);
    }

    private static void RunSituation(BurstTriggerSituation situation)
    {
        var trigger = new BurstTriggerI8(situation.Parameters);
        var edgeTriggerResults = new EdgeTriggerResults()
        {
            ArmIndices = new ulong[1000],
            TriggerIndices = new ulong[1000],
            CaptureEndIndices = new ulong[1000]
        };
        trigger.SetHorizontal(situation.WindowWidth, situation.WindowTriggerPosition, situation.AdditionalHoldoff);

        if (situation.ChunkCount > 1)
            throw new NotImplementedException();

        for (int i = 0; i < situation.ChunkCount; i++)
        {
            trigger.Process(situation.Input.Span.Slice((i * situation.ChunkSize), situation.ChunkSize), 0, ref edgeTriggerResults);
        }

        for (int i = 0; i < situation.ExpectedWindowEndIndices.Length; i++)
        {
            Assert.Equal(situation.ExpectedWindowEndIndices.Span[i], edgeTriggerResults.CaptureEndIndices[i]);
        }
    }
}
