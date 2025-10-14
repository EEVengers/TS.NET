namespace TS.NET.Tests;

internal class AnyEdgeTriggerSituationsI16
{
    public static EdgeTriggerSituationI16 SituationA_Rising()
    {
        var situation = new EdgeTriggerSituationI16()
        {
            Parameters = new EdgeTriggerParameters(Level: 20, Hysteresis: 10, EdgeDirection.Any),
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

    public static EdgeTriggerSituationI16 SituationA_Falling()
    {
        var situation = new EdgeTriggerSituationI16()
        {
            Parameters = new EdgeTriggerParameters(Level: -20, Hysteresis: 10, EdgeDirection.Any),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,
            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new short[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[1];
        situation.Input.Span.Fill(0);
        situation.Input.Span.Slice(100, 100).Fill(short.MinValue);
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        return situation;
    }

    public static EdgeTriggerSituationI16 SituationB_Rising()
    {
        var situation = new EdgeTriggerSituationI16()
        {
            Parameters = new EdgeTriggerParameters(Level: 20, Hysteresis: 10, EdgeDirection.Any),
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
        situation.Input.Span.Slice(300, 100).Fill(short.MaxValue);
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        return situation;
    }

    public static EdgeTriggerSituationI16 SituationB_Falling()
    {
        var situation = new EdgeTriggerSituationI16()
        {
            Parameters = new EdgeTriggerParameters(Level: -20, Hysteresis: 10, EdgeDirection.Any),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,
            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new short[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[1];
        situation.Input.Span.Fill(0);
        situation.Input.Span.Slice(100, 100).Fill(short.MinValue);
        situation.Input.Span.Slice(300, 100).Fill(short.MinValue);
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        return situation;
    }

    public static EdgeTriggerSituationI16 SituationC_Rising()
    {
        var situation = new EdgeTriggerSituationI16()
        {
            Parameters = new EdgeTriggerParameters(Level: 20, Hysteresis: 10, EdgeDirection.Any),
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

    public static EdgeTriggerSituationI16 SituationC_Falling()
    {
        var situation = new EdgeTriggerSituationI16()
        {
            Parameters = new EdgeTriggerParameters(Level: -20, Hysteresis: 10, EdgeDirection.Any),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,
            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new short[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[2];
        situation.Input.Span.Fill(0);
        situation.Input.Span.Slice(100, 100).Fill(short.MinValue);
        situation.Input.Span.Slice(10200, 100).Fill(short.MinValue);
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        situation.ExpectedWindowEndIndices.Span[1] = 20200;
        return situation;
    }

    public static EdgeTriggerSituationI16 SituationD_Rising()
    {
        var situation = new EdgeTriggerSituationI16()
        {
            Parameters = new EdgeTriggerParameters(Level: 20, Hysteresis: 10, EdgeDirection.Any),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,
            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new short[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[1];
        situation.Input.Span.Fill(-12800);
        situation.Input.Span.Slice(0, 100).Fill(short.MaxValue);
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        return situation;
    }

    public static EdgeTriggerSituationI16 SituationD_Falling()
    {
        var situation = new EdgeTriggerSituationI16()
        {
            Parameters = new EdgeTriggerParameters(Level: -20, Hysteresis: 10, EdgeDirection.Any),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,
            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new short[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[1];
        situation.Input.Span.Fill(12800);
        situation.Input.Span.Slice(0, 100).Fill(short.MinValue);
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        return situation;
    }

    public static EdgeTriggerSituationI16 SituationE_Rising()
    {
        var situation = new EdgeTriggerSituationI16()
        {
            Parameters = new EdgeTriggerParameters(Level: 20, Hysteresis: 10, EdgeDirection.Any),
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

    public static EdgeTriggerSituationI16 SituationE_Falling()
    {
        var situation = new EdgeTriggerSituationI16()
        {
            Parameters = new EdgeTriggerParameters(Level: -20, Hysteresis: 10, EdgeDirection.Any),
            WindowWidth = 10000,
            WindowTriggerPosition = 0,
            AdditionalHoldoff = 0,
            ChunkSize = 8388608,
            ChunkCount = 1
        };
        situation.Input = new short[situation.ChunkSize * situation.ChunkCount];
        situation.ExpectedWindowEndIndices = new int[1];
        situation.Input.Span.Fill(0);
        situation.Input.Span[100] = short.MinValue;
        situation.ExpectedWindowEndIndices.Span[0] = 10100;
        return situation;
    }
}
