namespace TS.NET.Engine;

public class DataDto
{
    public ulong SampleStartIndex;          // Index of first sample, counting from DMA enabled
    public int SampleLength;
    public ThunderscopeDataType MemoryType;
    public required ThunderscopeMemory Memory;
    public ThunderscopeHardwareConfig HardwareConfig;
}
