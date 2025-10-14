using System;

namespace TS.NET.Tests;

internal class EdgeTriggerSituationI16
{
    public EdgeTriggerParameters Parameters;
    public long WindowWidth;
    public long WindowTriggerPosition;
    public long AdditionalHoldoff;

    public int ChunkSize;
    public int ChunkCount;

    public Memory<short> Input;
    public Memory<int> ExpectedWindowEndIndices;
}
