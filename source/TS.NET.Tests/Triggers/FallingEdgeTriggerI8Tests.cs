using System;
using Xunit;

namespace TS.NET.Tests;

public class FallingEdgeTriggerI8Tests
{
    [Fact]
    public void SituationA()
    {
        var situation = FallingEdgeTriggerSituationsI8.SituationA();
        RunSituation(situation);
    }

    [Fact]
    public void SituationB()
    {
        var situation = FallingEdgeTriggerSituationsI8.SituationB();
        RunSituation(situation);
    }

    [Fact]
    public void SituationC()
    {
        var situation = FallingEdgeTriggerSituationsI8.SituationC();
        RunSituation(situation);
    }

    [Fact]
    public void SituationD()
    {
        var situation = FallingEdgeTriggerSituationsI8.SituationD();
        RunSituation(situation);
    }

    [Fact]
    public void SituationE()
    {
        var situation = FallingEdgeTriggerSituationsI8.SituationE();
        RunSituation(situation);
    }

    private static void RunSituation(EdgeTriggerSituationI8 situation)
    {
        var trigger = new FallingEdgeTriggerI8(situation.Parameters, 256);
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