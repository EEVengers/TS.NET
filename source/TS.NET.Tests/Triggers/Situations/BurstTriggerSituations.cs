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
    /// 1000 quiet samples followed by an arm sample and a rising crossing.
    /// </summary>
    public static BurstTriggerSituation SituationA()
    {
        var situation = new BurstTriggerSituation()
        {
            Parameters = new BurstTriggerParameters(LevelV: 20, Direction: BurstEdgeDirection.Rising, HysteresisPercent: 5, QuietUpperLevelV: 20, QuietLowerLevelV: -20, QuietTimeFs: 1_000_000_000),
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
        situation.Input.Span[2000] = 0;
        situation.ExpectedWindowEndIndices.Span[0] = 12001;
        return situation;
    }

    /// <summary>
    /// 999 quiet samples at index 1000 are ignored; the later quiet period arms and triggers.
    /// </summary>
    public static BurstTriggerSituation SituationB()
    {
        var situation = new BurstTriggerSituation()
        {
            Parameters = new BurstTriggerParameters(LevelV: 20, Direction: BurstEdgeDirection.Rising, HysteresisPercent: 5, QuietUpperLevelV: 20, QuietLowerLevelV: -20, QuietTimeFs: 1_000_000_000),
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
        situation.Input.Span[11000] = 0;
        situation.ExpectedWindowEndIndices.Span[0] = 21001;
        return situation;
    }
}
