namespace TS.NET.JTAG;

internal static class SpiFlashOpcodes
{
    internal const byte Read = 0x03;
    internal const byte ReadStatusRegister = 0x05;
    internal const byte WriteEnable = 0x06;
    internal const byte SectorErase = 0x20;
    internal const byte ReleasePowerDown = 0xAB;
    internal const byte ReadId = 0x9F;
    internal const byte PageProgram = 0x02;
}