using System;

namespace TS.NET.Tests;

internal class EdgeTriggerSituationI8
{
    public EdgeTriggerParameters Parameters;
    public long WindowWidth;
    public long WindowTriggerPosition;
    public long AdditionalHoldoff;

    public int ChunkSize;
    public int ChunkCount;

    public Memory<sbyte> Input;
    public Memory<int> ExpectedWindowEndIndices;
}
