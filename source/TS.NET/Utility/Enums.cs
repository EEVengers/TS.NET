namespace TS.NET;

public enum Channels : byte
{
    None = 0,
    One = 1,
    Two = 2,
    Four = 4
}

public enum TriggerChannel : byte
{
    None = 0,
    One = 1,
    Two = 2,
    Three = 3,
    Four = 4
}

public enum ChannelLength : ulong
{
    OneK = 1000,
    OneHundredM = 100000000
}

// Avoid using 0 value - makes it easier to find bugs [incorrect ser/deser and unset variables]

public enum TriggerMode : byte
{
    Auto = 1,
    Normal = 2,
    Single_Unarmed = 3,
    Single_Armed = 4,
}

public enum BoxcarLength
{
    None = 1,
    By2 = 2,
    By4 = 3,
    By8 = 4,
    By16 = 5,
    By32 = 6,
    By64 = 7,
    By128 = 8,
    By256 = 9,
    By512 = 10,
    By1024 = 11,
    By2048 = 12,
    By4096 = 13,
    By8192 = 14,
    By16384 = 15,
    By32768 = 16,
    By65536 = 17,
    By131072 = 18,
    By262144 = 19,
    By524288 = 20,
    By1048576 = 21,
    By2097152 = 22,
    By4194304 = 23,
    By8388608 = 24
};

public static class BoxcarUtility
{
    public static int ToDivisor(BoxcarLength boxcarLength)
    {
        return boxcarLength switch
        {
            BoxcarLength.None => 1,
            BoxcarLength.By2 => 2,
            BoxcarLength.By4 => 4,
            BoxcarLength.By8 => 8,
            BoxcarLength.By16 => 4,
            BoxcarLength.By32 => 5,
            BoxcarLength.By64 => 6,
            BoxcarLength.By128 => 7,
            BoxcarLength.By256 => 8,
            BoxcarLength.By512 => 9,
            BoxcarLength.By1024 => 10,
            BoxcarLength.By2048 => 11,
            BoxcarLength.By4096 => 12,
            BoxcarLength.By8192 => 13,
            BoxcarLength.By16384 => 14,
            BoxcarLength.By32768 => 15,
            BoxcarLength.By65536 => 16,
            BoxcarLength.By131072 => 17,
            BoxcarLength.By262144 => 18,
            BoxcarLength.By524288 => 19,
            BoxcarLength.By1048576 => 20,
            BoxcarLength.By2097152 => 21,
            BoxcarLength.By4194304 => 22,
            BoxcarLength.By8388608 => 23,
            _ => 1
        };
    }
}
