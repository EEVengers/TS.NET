namespace TS.NET.Hardware
{
    internal enum Hmcad15xxRegister : byte
    {
        SW_RST = 0x00,
        POWER_CTRL = 0x0F,
        CHAN_INVERT = 0x24,
        COARSE_GAIN_1 = 0x2A,
        COARSE_GAIN_2 = 0x2B,
        CHAN_MODE = 0x31,
        GAIN_SEL = 0x33,
        IN_SEL_1_2 = 0x3A,
        IN_SEL_3_4 = 0x3B,
        ADC_FULL_SCALE = 0x55,
        LVDS_MISC = 0x53,
        LCLK_PHASE = 0x42,
        LVDS_DRIVE = 0x11,
        DATA_FMT = 0x46
    }

    internal enum Hmcad15xxLvdsOutputDriveStrength : byte
    {
        Strength3_5mA = 0, // Default
        Strength2_5mA = 1,
        Strength1_5mA = 2, // RSDS, Reduced Swing Differential Signaling
        Strength0_5mA = 3,
        Strength7_5mA = 4,
        Strength6_5mA = 5,
        Strength5_5mA = 6,
        Strength4_5mA = 7
    }

    internal enum Hmcad15xxLvdsPhase : byte
    {
        Phase270Deg = 0,
        Phase180Deg = 1,
        Phase90Deg = 2,
        Phase0Deg = 3
    }

    internal enum Hmcad15xxLvdsLowClockFrequency : byte
    {
        Inactive = 0,
        Active = 1
    }

    internal enum Hmcad15xxLvdsDataWidth : byte
    {
        Width8Bit = 0,
        Width12Bit = 1,
        Width14Bit = 2
    }

    internal enum Hmcad15xxDataBtcFormat : byte
    {
        Offset = 0,
        TwosComplement = 1
    }

    internal enum Hmcad15xxMode : byte
    {
        SingleChannel = 1,
        DualChannel = 2,
        QuadChannel = 4,
        QuadChannel14bit = 8
    }

    internal enum Hmcad15xxClockDiv : byte
    {
        Div1 = 0,
        Div2 = 1,
        Div4 = 2,
        Div8 = 3
    }
}
