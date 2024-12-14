using System.Runtime.InteropServices;

namespace TS.NET.Driver.Libtslitex
{
    internal static partial class Interop
    {
        private const string library = "tslitex";
        
        [StructLayout(LayoutKind.Sequential)]
        public struct tsChannelParam_t
        {
            public uint volt_scale_mV;
            public int volt_offset_mV;
            public uint bandwidth;
            public byte coupling;
            public byte term;
            public byte active;
            public byte reserved;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct tsDeviceInfo_t
        {
            public uint deviceID;
            [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 256)]
            public string devicePath;
            [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 256)]
            public string identity;
            [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 256)]
            public string serialNumber;
        }

        
        [StructLayout(LayoutKind.Sequential)]
        public struct tsScopeState_t
        {
            public uint adc_sample_rate;
            public uint adc_sample_bits;
            public uint adc_sample_resolution;
            public uint adc_lost_buffer_count;
            public uint flags;
            public uint temp_c;
            public uint vcc_int;
            public uint vcc_aux;
            public uint vcc_bram;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct tsChannelCalibration_t
        {
            public int buffer_mV;
            public int bias_mV;
            public int attenuatorGain1M_mdB;
            public int attenuatorGain50_mdB;
            public int bufferGain_mdB;
            public int trimRheostat_range;
            public int preampLowGainError_mdB;
            public int preampHighGainError_mdB;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 11)]
            public int[] preampAttenuatorGain_mdB;
            public int preampOutputGainError_mdB;
            public int preampLowOffset_mV;
            public int preampHighOffset_mV;
            public int preampInputBias_uA;
        }

        [LibraryImport(library, EntryPoint = "thunderscopeOpen")]
        public static partial nint Open(uint devIndex);

        [LibraryImport(library, EntryPoint = "thunderscopeClose")]
        public static partial int Close(nint ts);

        [DllImport(library, EntryPoint = "thunderscopeListDevices")]        // Use runtime marshalling for now. Custom marshalling later.
        public static extern int ListDevices(uint devIndex, out tsDeviceInfo_t devInfo);

        [LibraryImport(library, EntryPoint = "thunderscopeChannelConfigGet")]
        public static partial int GetChannelConfig(nint ts, uint channel, out tsChannelParam_t conf);
        
        [LibraryImport(library, EntryPoint = "thunderscopeChannelConfigSet")]
        public static partial int SetChannelConfig(nint ts, uint channel, in tsChannelParam_t conf);

        [LibraryImport(library, EntryPoint = "thunderscopeStatusGet")]
        public static partial int GetStatus(nint ts, out tsScopeState_t conf);

        [LibraryImport(library, EntryPoint = "thunderscopeSampleModeSet")]
        public static partial int SetSampleMode(nint ts, uint rate, uint resolution);

        [DllImport(library, EntryPoint = "thunderscopeCalibrationSet")]     // Use runtime marshalling for now. Custom marshalling later.
        public static extern int SetCalibration(nint ts, uint channel, in tsChannelCalibration_t cal);

        [LibraryImport(library, EntryPoint = "thunderscopeDataEnable")]
        public static partial int DataEnable(nint ts, byte enable);

        [LibraryImport(library, EntryPoint = "thunderscopeRead")]
        public static unsafe partial int Read(nint ts, byte* buffer, uint len);
    }
}