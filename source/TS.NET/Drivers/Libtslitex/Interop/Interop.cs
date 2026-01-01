using System;
using System.Runtime.InteropServices;

namespace TS.NET.Driver.Libtslitex
{
    internal static partial class Interop
    {
        private const string library = "tslitex";
        
        [StructLayout(LayoutKind.Sequential)]
        public struct tsChannelParam_t
        {
            public uint volt_scale_uV;
            public int volt_offset_uV;
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
            public uint hw_id;
            public uint gw_id;
            public uint litex;
            public uint board_rev;
            [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 256)]
            public string devicePath;
            [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 256)]
            public string identity;
            [MarshalAs (UnmanagedType.ByValTStr, SizeConst = 256)]
            public string serialNumber;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string buildConfig;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string buildDate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string mfgSignature;
        }


        [StructLayout(LayoutKind.Sequential)]
        public struct tsScopeState_t
        {
            public uint adc_sample_rate;
            public uint adc_sample_bits;

            public uint adc_sample_resolution;
            public uint adc_lost_buffer_count;
            public uint flags;

            //sysHealth_t
            public uint temp_c;
            public uint vcc_int;
            public uint vcc_aux;
            public uint vcc_bram;
            public byte frontend_power_good;
            public byte acq_power_good;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct tsChannelCalibration_t
        {
            public int buffer_uV;
            public int bias_uV;
            public int attenuatorGain1M_mdB;
            public int attenuatorGain50_mdB;
            public int bufferGain_mdB;
            public int trimRheostat_range;
            public int preampLowGainError_mdB;
            public int preampHighGainError_mdB;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 11)]
            public int[] preampAttenuatorGain_mdB;
            public int preampOutputGainError_mdB;
            public int preampLowOffset_uV;
            public int preampHighOffset_uV;
            public int preampInputBias_uA;

            public tsChannelCalibration_t() { preampAttenuatorGain_mdB = new int[11]; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct tsAdcCalibration_t
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] branchFineGain;

            public tsAdcCalibration_t() { branchFineGain = new byte[8]; }
        }

        public enum tsSampleFormat_t
        {
            Format8Bit = 0,
            Format12BitLSB,
            Format12BitMSB,
            Format14Bit
        }

        public enum tsEventType_t
        {
            TS_EVT_NONE = 0,
            TS_EVT_HOST_SW,
            TS_EVT_EXT_SYNC,
        }

        public struct tsEvent_t
        {
            tsEventType_t ID;
            ulong event_sample;
        }

        [DllImport(library, EntryPoint = "thunderscopeListDevices")]        // Use runtime marshalling for now. Custom marshalling later.
        public static extern int ListDevices(uint devIndex, out tsDeviceInfo_t devInfo);

        [LibraryImport(library, EntryPoint = "thunderscopeOpen")]
        public static partial nint Open(uint devIndex, [MarshalAs(UnmanagedType.U1)] bool skip_init);

        [LibraryImport(library, EntryPoint = "thunderscopeClose")]
        public static partial int Close(nint ts);

        [LibraryImport(library, EntryPoint = "thunderscopeChannelConfigGet")]
        public static partial int GetChannelConfig(nint ts, uint channel, out tsChannelParam_t conf);
        
        [LibraryImport(library, EntryPoint = "thunderscopeChannelConfigSet")]
        public static partial int SetChannelConfig(nint ts, uint channel, in tsChannelParam_t conf);

        [LibraryImport(library, EntryPoint = "thunderscopeStatusGet")]
        public static partial int GetStatus(nint ts, out tsScopeState_t conf);

        [LibraryImport(library, EntryPoint = "thunderscopeSampleModeSet")]
        public static partial int SetSampleMode(nint ts, uint rate, tsSampleFormat_t mode);

        [LibraryImport(library, EntryPoint = "thunderscopeDataEnable")]
        public static partial int DataEnable(nint ts, byte enable);

        [LibraryImport(library, EntryPoint = "thunderscopeRead")]
        public static unsafe partial int Read(nint ts, byte* buffer, uint len);

        [LibraryImport(library, EntryPoint = "thunderscopeReadCount")]
        public static unsafe partial int Read(nint ts, byte* buffer, uint len, out ulong count);

        [LibraryImport(library, EntryPoint = "thunderscopeEventGet")]
        public static unsafe partial int GetEvent(nint ts, out tsEvent_t evt);

        [LibraryImport(library, EntryPoint = "thunderscopeFwUpdate")]
        public static unsafe partial int FirmwareUpdate(nint ts, byte* bitstream, uint len);

        [LibraryImport(library, EntryPoint = "thunderscopeGetFwProgress")]
        public static unsafe partial int FirmwareUpdateProgress(nint ts, out uint progress);

        [LibraryImport(library, EntryPoint = "thunderscopeUserDataRead")]
        public static unsafe partial int UserDataRead(nint ts, byte* buffer, uint offset, uint readLen);

        [LibraryImport(library, EntryPoint = "thunderscopeUserDataWrite")]
        public static unsafe partial int UserDataWrite(nint ts, byte* buffer, uint offset, uint writeLen);

        [DllImport(library, EntryPoint = "thunderscopeChanCalibrationSet")]     // Use runtime marshalling for now. Custom marshalling later.
        public static extern int SetCalibration(nint ts, uint channel, in tsChannelCalibration_t cal);
        
        [DllImport(library, EntryPoint = "thunderscopeAdcCalibrationSet")]      // Use runtime marshalling for now. Custom marshalling later.
        public static extern int SetAdcCalibration(nint ts, in tsAdcCalibration_t cal);

        [DllImport(library, EntryPoint = "thunderscopeAdcCalibrationGet")]      // Use runtime marshalling for now. Custom marshalling later.
        public static extern int GetAdcCalibration(nint ts, out tsAdcCalibration_t cal);

        [StructLayout(LayoutKind.Sequential)]
        public struct tsChannelCtrl_t
        {
            public byte atten;
            public byte term;
            public byte dc_couple;
            public byte dpot;
            public ushort dac;
            public byte pga_high_gain;
            public byte pga_atten;
            public byte pga_bw;
        }

        [LibraryImport(library, EntryPoint = "thunderscopeCalibrationManualCtrl")]
        public static unsafe partial int SetChannelManualControl(nint ts, uint channel, in tsChannelCtrl_t ctrl);

        // Factory methods
        [LibraryImport(library, EntryPoint = "thunderscopeFactoryProvisionPrepare")]
        public static unsafe partial int FactoryDataErase(nint ts, ulong dna);

        [LibraryImport(library, EntryPoint = "thunderscopeFactoryProvisionAppendTLV")]
        public static unsafe partial int FactoryDataAppend(nint ts, uint tag, uint length, byte* content);
    }
}
