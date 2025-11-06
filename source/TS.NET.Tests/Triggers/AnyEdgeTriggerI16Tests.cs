using System;
using Xunit;

namespace TS.NET.Tests;

public class AnyEdgeTriggerI16Tests
{
    [Fact]
    public void SituationA_Rising()
    {
        var situation = AnyEdgeTriggerSituationsI16.SituationA_Rising();
        RunSituation(situation);
    }

    [Fact]
    public void SituationB_Rising()
    {
        var situation = AnyEdgeTriggerSituationsI16.SituationB_Rising();
        RunSituation(situation);
    }

    [Fact]
    public void SituationC_Rising()
    {
        var situation = AnyEdgeTriggerSituationsI16.SituationC_Rising();
        RunSituation(situation);
    }

    [Fact]
    public void SituationD_Rising()
    {
        var situation = AnyEdgeTriggerSituationsI16.SituationD_Rising();
        RunSituation(situation);
    }

    [Fact]
    public void SituationE_Rising()
    {
        var situation = AnyEdgeTriggerSituationsI16.SituationE_Rising();
        RunSituation(situation);
    }

    [Fact]
    public void SituationA_Falling()
    {
        var situation = AnyEdgeTriggerSituationsI16.SituationA_Falling();
        RunSituation(situation);
    }

    [Fact]
    public void SituationB_Falling()
    {
        var situation = AnyEdgeTriggerSituationsI16.SituationB_Falling();
        RunSituation(situation);
    }

    [Fact]
    public void SituationC_Falling()
    {
        var situation = AnyEdgeTriggerSituationsI16.SituationC_Falling();
        RunSituation(situation);
    }

    [Fact]
    public void SituationD_Falling()
    {
        var situation = AnyEdgeTriggerSituationsI16.SituationD_Falling();
        RunSituation(situation);
    }

    [Fact]
    public void SituationE_Falling()
    {
        var situation = AnyEdgeTriggerSituationsI16.SituationE_Falling();
        RunSituation(situation);
    }

    private static void RunSituation(EdgeTriggerSituationI16 situation)
    {
        var trigger = new AnyEdgeTriggerI16(situation.Parameters, AdcResolution.TwelveBit, 256);
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
