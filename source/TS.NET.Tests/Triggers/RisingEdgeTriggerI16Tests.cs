using System;
using Xunit;

namespace TS.NET.Tests;

public class RisingEdgeTriggerI16Tests
{
    [Fact]
    public void SituationA()
    {
        var situation = RisingEdgeTriggerSituationsI16.SituationA();
        RunSituation(situation);
    }

    [Fact]
    public void SituationB()
    {
        var situation = RisingEdgeTriggerSituationsI16.SituationB();
        RunSituation(situation);
    }

    [Fact]
    public void SituationC()
    {
        var situation = RisingEdgeTriggerSituationsI16.SituationC();
        RunSituation(situation);
    }

    [Fact]
    public void SituationD()
    {
        var situation = RisingEdgeTriggerSituationsI16.SituationD();
        RunSituation(situation);
    }

    [Fact]
    public void SituationE()
    {
        var situation = RisingEdgeTriggerSituationsI16.SituationE();
        RunSituation(situation);
    }

    private static void RunSituation(EdgeTriggerSituationI16 situation)
    {
        var trigger = new RisingEdgeTriggerI16(situation.Parameters);
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
