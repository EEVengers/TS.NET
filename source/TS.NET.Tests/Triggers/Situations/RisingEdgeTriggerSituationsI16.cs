namespace TS.NET.Tests;

internal class RisingEdgeTriggerSituationsI16
{
    /// <summary>
    /// 100 samples idle, 100 sample wide positive pulse
    /// </summary>
    public static EdgeTriggerSituationI16 SituationA()
    {
        var situation = new EdgeTriggerSituationI16()
        {
            Parameters = new EdgeTriggerParameters(LevelV: 20, HysteresisPercent: 5, EdgeDirection.Rising),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,

            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new short[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[1];
        situation.Input.Span.Fill(0);
        situation.Input.Span.Slice(100, 100).Fill(short.MaxValue);
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        return situation;
    }

    /// <summary>
    /// 100 samples idle, 100 sample wide positive pulse, 100 samples idle, 100 sample wide positive pulse. Second pulse should be ignored.
    /// </summary>
    public static EdgeTriggerSituationI16 SituationB()
    {
        var situation = new EdgeTriggerSituationI16()
        {
            Parameters = new EdgeTriggerParameters(LevelV: 20, HysteresisPercent: 5, EdgeDirection.Rising),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,

            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new short[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[1];
        situation.Input.Span.Fill(0);
        situation.Input.Span.Slice(100, 100).Fill(short.MaxValue);
        situation.Input.Span.Slice(300, 100).Fill(short.MaxValue);  // This pulse should be ignored
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        return situation;
    }

    /// <summary>
    /// 100 samples idle, 100 sample wide positive pulse, 10000 samples idle, 100 sample wide positive pulse.
    /// </summary>
    public static EdgeTriggerSituationI16 SituationC()
    {
        var situation = new EdgeTriggerSituationI16()
        {
            Parameters = new EdgeTriggerParameters(LevelV: 20, HysteresisPercent: 5, EdgeDirection.Rising),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,

            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new short[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[2];
        situation.Input.Span.Fill(0);
        situation.Input.Span.Slice(100, 100).Fill(short.MaxValue);
        situation.Input.Span.Slice(10200, 100).Fill(short.MaxValue);
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        situation.ExpectedWindowEndIndices.Span[1] = 20200;
        return situation;
    }

    /// <summary>
    /// 100 sample wide positive pulse
    /// </summary>
    public static EdgeTriggerSituationI16 SituationD()
    {
        var situation = new EdgeTriggerSituationI16()
        {
            Parameters = new EdgeTriggerParameters(LevelV: 20, HysteresisPercent: 5, EdgeDirection.Rising),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,

            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new short[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[1];
        situation.Input.Span.Fill(0);
        situation.Input.Span.Slice(0, 100).Fill(short.MaxValue);
        situation.ExpectedWindowEndIndices.Span[0] = 0;
        return situation;
    }

    /// <summary>
    /// 100 samples idle, 1 sample wide positive pulse
    /// </summary>
    public static EdgeTriggerSituationI16 SituationE()
    {
        var situation = new EdgeTriggerSituationI16()
        {
            Parameters = new EdgeTriggerParameters(LevelV: 20, HysteresisPercent: 5, EdgeDirection.Rising),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,

            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new short[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[1];
        situation.Input.Span.Fill(0);
        situation.Input.Span[100] = short.MaxValue;
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        return situation;
    }
}
