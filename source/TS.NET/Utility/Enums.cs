namespace TS.NET;

public enum AdcChannelMode : byte
{
    Single = 1,
    Dual = 2,
    Quad = 4
}

public enum ChannelLength : ulong
{
    OneK = 1000,
    OneHundredM = 100000000
}

public enum TriggerChannel : byte
{
    None = 0,
    One = 1,
    Two = 2,
    Three = 3,
    Four = 4
}

public enum TriggerMode : byte
{
    Auto = 1,
    Normal = 2,
    Single = 3,
    //Single_Armed?
    //Single_Unarmed?
}

public enum TriggerType : byte
{
    RisingEdge = 1,
    FallingEdge = 2
}

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
