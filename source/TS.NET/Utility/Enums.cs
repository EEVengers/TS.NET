namespace TS.NET;

public enum Channels
{
    None = 0,
    One = 1,
    Two = 2,
    Four = 4
}

public enum TriggerChannel
{
    None = 0,
    One = 1,
    Two = 2,
    Three = 3,
    Four = 4
}

public enum TriggerMode
{
    Auto,
    Normal,
    Single_Unarmed,
    Single_Armed,
}

public enum BoxcarLength
{
    None = 0,
    By2 = 1,
    By4 = 2,
    By8 = 3,
    By16 = 4,
    By32 = 5,
    By64 = 6,
    By128 = 7,
    By256 = 8,
    By512 = 9,
    By1024 = 10,
    By2048 = 11,
    By4096 = 12,
    By8192 = 13,
    By16384 = 14,
    By32768 = 15,
    By65536 = 16,
    By131072 = 17,
    By262144 = 18,
    By524288 = 19,
    By1048576 = 20,
    By2097152 = 21,
    By4194304 = 22,
    By8388608 = 23
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
