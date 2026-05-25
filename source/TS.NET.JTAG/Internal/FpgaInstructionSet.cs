namespace TS.NET.JTAG;

internal sealed class XilinxInstructionSet
{
    internal int IrLength { get; init; }
    internal ushort IdCodeInstruction { get; init; }
    internal ushort IscEnableInstruction { get; init; }
    internal ushort IscDisableInstruction { get; init; }
    internal ushort DnaInstruction { get; init; }
    internal bool DnaRequiresIscEnable { get; init; }
    internal bool DnaReverseBits { get; init; }
    internal int DnaReadBitLength { get; init; }
    internal int DnaBitLength { get; init; }
    internal int DnaPreReadIdleClocks { get; init; }
    internal int DnaPostReadIdleClocks { get; init; }
    internal int DnaPostDisableIdleClocks { get; init; }
    internal ushort JProgramInstruction { get; init; }
    internal ushort JShutdownInstruction { get; init; }
    internal ushort CfgInInstruction { get; init; }
    internal ushort JStartInstruction { get; init; }
    internal ushort User1Instruction { get; init; }
    internal ushort UserCodeInstruction { get; init; }
    internal int FlashPageSizeBytes { get; init; }
    internal int FlashSectorSizeBytes { get; init; }
    internal int ProgramShutdownIdleClocks { get; init; }
    internal int ProgramPostJProgramDelayMs { get; init; }
    internal int ProgramPostCfgInIdleClocks { get; init; }
    internal int PostProgramIdleClocks { get; init; }

    internal static XilinxInstructionSet XC7DnaPort { get; } = new()
    {
        IrLength = 6,
        IdCodeInstruction = 0x09,
        IscEnableInstruction = 0x10,
        IscDisableInstruction = 0x16,
        DnaInstruction = 0x17,
        DnaRequiresIscEnable = true,
        DnaReverseBits = true,
        DnaReadBitLength = 64,
        DnaBitLength = 57,
        DnaPreReadIdleClocks = 64,
        DnaPostReadIdleClocks = 64,
        DnaPostDisableIdleClocks = 64,
        JProgramInstruction = 0x0B,
        JShutdownInstruction = 0x0D,
        CfgInInstruction = 0x05,
        JStartInstruction = 0x0C,
        User1Instruction = 0x02,
        UserCodeInstruction = 0x08,
        FlashPageSizeBytes = 256,
        FlashSectorSizeBytes = 4096,
        ProgramShutdownIdleClocks = 16,
        ProgramPostJProgramDelayMs = 10,
        ProgramPostCfgInIdleClocks = 1,
        PostProgramIdleClocks = 2048,
    };

    internal static XilinxInstructionSet XC7FuseDna { get; } = new()
    {
        IrLength = 6,
        IdCodeInstruction = 0x09,
        IscEnableInstruction = 0x10,
        IscDisableInstruction = 0x16,
        DnaInstruction = 0x32,
        DnaRequiresIscEnable = false,
        DnaReverseBits = false,
        DnaReadBitLength = 64,
        DnaBitLength = 64,
        DnaPreReadIdleClocks = 0,
        DnaPostReadIdleClocks = 0,
        DnaPostDisableIdleClocks = 0,
        JProgramInstruction = 0x0B,
        JShutdownInstruction = 0x0D,
        CfgInInstruction = 0x05,
        JStartInstruction = 0x0C,
        User1Instruction = 0x02,
        UserCodeInstruction = 0x08,
        FlashPageSizeBytes = 256,
        FlashSectorSizeBytes = 4096,
        ProgramShutdownIdleClocks = 16,
        ProgramPostJProgramDelayMs = 10,
        ProgramPostCfgInIdleClocks = 1,
        PostProgramIdleClocks = 2048,
    };
}
