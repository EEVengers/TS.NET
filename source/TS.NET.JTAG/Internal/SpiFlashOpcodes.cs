namespace TS.NET.JTAG;

internal static class SpiFlashOpcodes
{
    internal const byte Read = 0x03;
    internal const byte ReadStatusRegister = 0x05;
    internal const byte WriteEnable = 0x06;
    internal const byte SectorErase = 0x20;
    internal const byte EnableReset = 0x66;
    internal const byte ResetMemory = 0x99;
    internal const byte Exit4ByteAddressMode = 0xE9;
    internal const byte ReleasePowerDown = 0xAB;
    internal const byte ReadId = 0x9F;
    internal const byte PageProgram = 0x02;
}