using System;

namespace TS.NET.Tests;

internal class BurstTriggerSituation
{
    public BurstTriggerParameters Parameters;
    public long WindowWidth;
    public long WindowTriggerPosition;
    public long AdditionalHoldoff;

    public int ChunkSize;
    public int ChunkCount;

    public Memory<sbyte> Input;
    public Memory<ulong> ExpectedWindowEndIndices;
}

internal class BurstTriggerSituations
{
    /// <summary>
    /// 1000 sample waveform at index 1000
    /// </summary>
    public static BurstTriggerSituation SituationA()
    {
        var situation = new BurstTriggerSituation()
        {
            Parameters = new BurstTriggerParameters(WindowHighLevel: 20, WindowLowLevel: -20, MinimumInRangePeriod: 1000),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,

            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new ulong[1];
        situation.Input.Span.Fill(sbyte.MaxValue);
        situation.Input.Span.Slice(1000, 1000).Fill(0);
        situation.ExpectedWindowEndIndices.Span[0] = 12000;
        return situation;
    }

    /// <summary>
    /// 999 sample waveform at index 1000, to be ignored, 1000 wide waveform at index 10000
    /// </summary>
    public static BurstTriggerSituation SituationB()
    {
        var situation = new BurstTriggerSituation()
        {
            Parameters = new BurstTriggerParameters(WindowHighLevel: 20, WindowLowLevel: -20, MinimumInRangePeriod: 1000),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,

            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new ulong[1];
        situation.Input.Span.Fill(sbyte.MaxValue);
        situation.Input.Span.Slice(1000, 995).Fill(0);       // Should be ignored
        situation.Input.Span.Slice(10000, 1000).Fill(0);
        situation.ExpectedWindowEndIndices.Span[0] = 21000;
        return situation;
    }
}
