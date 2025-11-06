namespace TS.NET.Tests;

internal class AnyEdgeTriggerSituationsI8
{
    public static EdgeTriggerSituationI8 SituationA_Rising()
    {
        var situation = new EdgeTriggerSituationI8()
        {
            Parameters = new EdgeTriggerParameters(LevelV: 20, HysteresisPercent: 5, EdgeDirection.Any),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,
            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[1];
        situation.Input.Span.Fill(0);
        situation.Input.Span.Slice(100, 100).Fill(sbyte.MaxValue);
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        return situation;
    }

    public static EdgeTriggerSituationI8 SituationA_Falling()
    {
        var situation = new EdgeTriggerSituationI8()
        {
            Parameters = new EdgeTriggerParameters(LevelV: -20, HysteresisPercent: 5, EdgeDirection.Any),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,
            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[1];
        situation.Input.Span.Fill(0);
        situation.Input.Span.Slice(100, 100).Fill(sbyte.MinValue);
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        return situation;
    }

    public static EdgeTriggerSituationI8 SituationB_Rising()
    {
        var situation = new EdgeTriggerSituationI8()
        {
            Parameters = new EdgeTriggerParameters(LevelV: 20, HysteresisPercent: 5, EdgeDirection.Any),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,
            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[1];
        situation.Input.Span.Fill(0);
        situation.Input.Span.Slice(100, 100).Fill(sbyte.MaxValue);
        situation.Input.Span.Slice(300, 100).Fill(sbyte.MaxValue);
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        return situation;
    }

    public static EdgeTriggerSituationI8 SituationB_Falling()
    {
        var situation = new EdgeTriggerSituationI8()
        {
            Parameters = new EdgeTriggerParameters(LevelV: -20, HysteresisPercent: 5, EdgeDirection.Any),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,
            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[1];
        situation.Input.Span.Fill(0);
        situation.Input.Span.Slice(100, 100).Fill(sbyte.MinValue);
        situation.Input.Span.Slice(300, 100).Fill(sbyte.MinValue);
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        return situation;
    }

    public static EdgeTriggerSituationI8 SituationC_Rising()
    {
        var situation = new EdgeTriggerSituationI8()
        {
            Parameters = new EdgeTriggerParameters(LevelV: 20, HysteresisPercent: 5, EdgeDirection.Any),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,
            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[2];
        situation.Input.Span.Fill(0);
        situation.Input.Span.Slice(100, 100).Fill(sbyte.MaxValue);
        situation.Input.Span.Slice(10200, 100).Fill(sbyte.MaxValue);
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        situation.ExpectedWindowEndIndices.Span[1] = 20200;
        return situation;
    }

    public static EdgeTriggerSituationI8 SituationC_Falling()
    {
        var situation = new EdgeTriggerSituationI8()
        {
            Parameters = new EdgeTriggerParameters(LevelV: -20, HysteresisPercent: 5, EdgeDirection.Any),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,
            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[2];
        situation.Input.Span.Fill(0);
        situation.Input.Span.Slice(100, 100).Fill(sbyte.MinValue);
        situation.Input.Span.Slice(10200, 100).Fill(sbyte.MinValue);
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        situation.ExpectedWindowEndIndices.Span[1] = 20200;
        return situation;
    }

    public static EdgeTriggerSituationI8 SituationD_Rising()
    {
        var situation = new EdgeTriggerSituationI8()
        {
            Parameters = new EdgeTriggerParameters(LevelV: 20, HysteresisPercent: 5, EdgeDirection.Any),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,
            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[1];
        situation.Input.Span.Fill(-50);
        situation.Input.Span.Slice(0, 100).Fill(sbyte.MaxValue);
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        return situation;
    }

    public static EdgeTriggerSituationI8 SituationD_Falling()
    {
        var situation = new EdgeTriggerSituationI8()
        {
            Parameters = new EdgeTriggerParameters(LevelV: -20, HysteresisPercent: 5, EdgeDirection.Any),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,
            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[1];
        situation.Input.Span.Fill(50);
        situation.Input.Span.Slice(0, 100).Fill(sbyte.MinValue);
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        return situation;
    }

    public static EdgeTriggerSituationI8 SituationE_Rising()
    {
        var situation = new EdgeTriggerSituationI8()
        {
            Parameters = new EdgeTriggerParameters(LevelV: 20, HysteresisPercent: 5, EdgeDirection.Any),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,
            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[1];
        situation.Input.Span.Fill(0);
        situation.Input.Span[100] = sbyte.MaxValue;
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        return situation;
    }

    public static EdgeTriggerSituationI8 SituationE_Falling()
    {
        var situation = new EdgeTriggerSituationI8()
        {
            Parameters = new EdgeTriggerParameters(LevelV: -20, HysteresisPercent: 5, EdgeDirection.Any),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,
            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[1];
        situation.Input.Span.Fill(0);
        situation.Input.Span[100] = sbyte.MinValue;
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        return situation;
    }
}
