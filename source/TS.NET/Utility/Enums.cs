namespace TS.NET;

public enum ChannelLength : ulong
{
    OneK = 1000,
    OneHundredM = 100000000
}

public enum TriggerChannel : byte
{
    NotSet = 0,
    Channel1 = 1,
    Channel2 = 2,
    Channel3 = 3,
    Channel4 = 4
}

public enum TriggerMode : byte
{
    Auto = 1,
    Normal = 2,
    Single = 3,         // Single effectively becomes Normal if Run again after a successful Single capture
    Stream = 4
    //Single_Armed?
    //Single_Unarmed?
}

public enum TriggerType : byte
{
    RisingEdge = 1,
    FallingEdge = 2
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

public enum ThunderscopeChannelDataType : byte
{
    U8 = 1,
    I8 = 2
}

public static class ThunderscopeChannelDataTypeExtensions
{
    public static uint Width(this ThunderscopeChannelDataType type)
    {
        return type switch
        {
            ThunderscopeChannelDataType.U8 => 1,
            ThunderscopeChannelDataType.I8 => 1,
            _ => throw new NotImplementedException()
        };
    }
}
