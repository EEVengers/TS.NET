namespace TS.NET;

public enum ChannelLength : ulong
{
    OneK = 1000,
    OneHundredM = 100000000
}

public enum TriggerChannel : byte
{
    None = 0,
    Channel1 = 1,
    Channel2 = 2,
    Channel3 = 3,
    Channel4 = 4
}

public enum Mode : byte
{
    Auto = 1,
    Normal = 2,
    Single = 3,
    Stream = 4
    //Single_Armed?
    //Single_Unarmed?
}

public enum TriggerType : byte
{
    // Triggers with vertical parameter(s)
    Edge = 1,
    Window = 2,
    // Triggers with vertical & time parameter(s)
    Runt = 3,
    Width = 4,
    Interval = 5,
    Burst = 6,
    Dropout = 7,
    SlewRate = 8,
    // FPGA provided event
    Event = 9
}

// 9 bit =     4x sum, 1x >>
// 10 bit =   16x sum, 2x >>
// 11 bit =   64x sum, 3x >>
// 12 bit =  256x sum, 4x >>
// 13 bit = 1024x sum, 5x >>
// 14 bit = 4096x sum, 6x >>
public enum BoxcarAveraging : uint
{
    None = 0,
    Average2 = 2,
    Average4 = 4,           // ENOB+1
    Average8 = 8,
    Average16 = 16,         // ENOB+2
    Average32 = 32,
    Average64 = 64,         // ENOB+3
    Average128 = 128,
    Average256 = 256,       // ENOB+4
    Average512 = 512,
    Average1024 = 1024,     // ENOB+5
    Average2048 = 2048,
    Average4096 = 4096,     // ENOB+6
    Average8192 = 8192,
    Average16384 = 16384,   // ENOB+7
    Average32768 = 32768,
    Average65536 = 65536,   // ENOB+8
};

public enum ThunderscopeDataType : byte
{
    U8 = 1,
    I8 = 2,
    U16 = 3,
    I16 = 4,
    U32 = 5,
    I32 = 6,
    F32 = 7,
    U64 = 8,
    I64 = 9,
    F64 = 10,
}

public static class ThunderscopeChannelDataTypeExtensions
{
    public static byte ByteWidth(this ThunderscopeDataType type)
    {
        return type switch
        {
            ThunderscopeDataType.U8 => 1,
            ThunderscopeDataType.I8 => 1,
            ThunderscopeDataType.U16 => 2,
            ThunderscopeDataType.I16 => 2,
            ThunderscopeDataType.U32 => 4,
            ThunderscopeDataType.I32 => 4,
            ThunderscopeDataType.F32 => 4,
            ThunderscopeDataType.U64 => 8,
            ThunderscopeDataType.I64 => 8,
            ThunderscopeDataType.F64 => 8,
            _ => throw new NotImplementedException()
        };
    }
}
