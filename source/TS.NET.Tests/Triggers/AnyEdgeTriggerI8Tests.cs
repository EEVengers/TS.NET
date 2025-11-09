using System;
using Xunit;

namespace TS.NET.Tests;

public class AnyEdgeTriggerI8Tests
{
    [Fact]
    public void SituationA_Rising()
    {
        var situation = AnyEdgeTriggerSituationsI8.SituationA_Rising();
        RunSituation(situation);
    }

    [Fact]
    public void SituationB_Rising()
    {
        var situation = AnyEdgeTriggerSituationsI8.SituationB_Rising();
        RunSituation(situation);
    }

    [Fact]
    public void SituationC_Rising()
    {
        var situation = AnyEdgeTriggerSituationsI8.SituationC_Rising();
        RunSituation(situation);
    }

    [Fact]
    public void SituationD_Rising()
    {
        var situation = AnyEdgeTriggerSituationsI8.SituationD_Rising();
        RunSituation(situation);
    }

    [Fact]
    public void SituationE_Rising()
    {
        var situation = AnyEdgeTriggerSituationsI8.SituationE_Rising();
        RunSituation(situation);
    }

    [Fact]
    public void SituationA_Falling()
    {
        var situation = AnyEdgeTriggerSituationsI8.SituationA_Falling();
        RunSituation(situation);
    }

    [Fact]
    public void SituationB_Falling()
    {
        var situation = AnyEdgeTriggerSituationsI8.SituationB_Falling();
        RunSituation(situation);
    }

    [Fact]
    public void SituationC_Falling()
    {
        var situation = AnyEdgeTriggerSituationsI8.SituationC_Falling();
        RunSituation(situation);
    }

    [Fact]
    public void SituationD_Falling()
    {
        var situation = AnyEdgeTriggerSituationsI8.SituationD_Falling();
        RunSituation(situation);
    }

    [Fact]
    public void SituationE_Falling()
    {
        var situation = AnyEdgeTriggerSituationsI8.SituationE_Falling();
        RunSituation(situation);
    }

    private static void RunSituation(EdgeTriggerSituationI8 situation)
    {
        var trigger = new AnyEdgeTriggerI8(situation.Parameters, 256);
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
            trigger.Process(situation.Input.Span.Slice((int)(i * situation.ChunkSize), (int)situation.ChunkSize), 0, ref edgeTriggerResults);
        }

        for (int i = 0; i < situation.ExpectedWindowEndIndices.Length; i++)
        {
            Assert.Equal(situation.ExpectedWindowEndIndices.Span[i], edgeTriggerResults.CaptureEndIndices[i]);
        }
    }
}
