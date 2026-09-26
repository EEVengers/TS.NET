using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TS.NET.Driver.Libtslitex
{
    public enum LibraryLocation
    {
        WorkingDirectory,
        LocalApplicationData
    }

    internal static partial class Interop
    {
        private const string library = "tslitex";

        internal static LibraryLocation? LoadedLibraryLocation { get; private set; }

        [ModuleInitializer]
        [SuppressMessage("Usage", "CA2255", Justification = "The resolver must be registered before the first native import is invoked.")]
        internal static void InitializeNativeLibraryResolver()
        {
            NativeLibrary.SetDllImportResolver(typeof(Interop).Assembly, ResolveLibrary);
        }

        private static nint ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (!OperatingSystem.IsWindows() || !string.Equals(libraryName, library, StringComparison.Ordinal))
            {
                return nint.Zero;
            }

            if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out nint handle))
                return RecordLoadedLibrary(handle, LibraryLocation.WorkingDirectory);

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var localApplicationDataConfigurationFile = Path.Combine(localAppData, "Programs", "ThunderScope", "libtslitex", libraryName);
            return NativeLibrary.TryLoad(localApplicationDataConfigurationFile, out handle)
                ? RecordLoadedLibrary(handle, LibraryLocation.LocalApplicationData)
                : nint.Zero;
        }

        private static nint RecordLoadedLibrary(nint handle, LibraryLocation source)
        {
            LoadedLibraryLocation = source;
            return handle;
        }

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
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string devicePath;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string identity;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
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
        public struct tsAfePathCalibration_s
        {
            public double bufferInputVpp;
            public double trimOffsetDacZeroC;
            public double trimOffsetDacZeroM;
            public double trimOffsetDacScale;
            public uint trimDPot;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct tsFrontendCalibration_t
        {
            public double attenuatorScale;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = 11)]
            public tsAfePathCalibration_s[] highPgaPathCal;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = 11)]
            public tsAfePathCalibration_s[] lowPgaPathCal;

            public tsFrontendCalibration_t()
            {
                highPgaPathCal = new tsAfePathCalibration_s[11];
                lowPgaPathCal = new tsAfePathCalibration_s[11];
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct tsAdcLoad_t
        {
            public uint rate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public double[] scale;

            public tsAdcLoad_t() { scale = new double[4]; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct tsAdcLoadCal_t
        {
            public uint channels;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = 8)]
            public tsAdcLoad_t[] conf;

            public tsAdcLoadCal_t()
            {
                conf = new tsAdcLoad_t[8];
                for (var cal = 0; cal < 8; cal++)
                {
                    conf[cal] = new tsAdcLoad_t();
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct tsAdcGain_t
        {
            public uint rate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] gain;

            public tsAdcGain_t() { gain = new byte[8]; }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct tsAdcGainCal_t
        {
            public uint channels;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = 8)]
            public tsAdcGain_t[] conf;

            public tsAdcGainCal_t()
            {
                conf = new tsAdcGain_t[8];
                for (var cal = 0; cal < 8; cal++)
                {
                    conf[cal] = new tsAdcGain_t();
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct tsAdcCalibration_t
        {
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = 11)]
            public tsAdcLoadCal_t[] loadCal;
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.Struct, SizeConst = 11)]
            public tsAdcGainCal_t[] branchFineGain;

            public tsAdcCalibration_t()
            {
                loadCal = new tsAdcLoadCal_t[11];
                branchFineGain = new tsAdcGainCal_t[11];
                for (var cal = 0; cal < 11; cal++)
                {
                    loadCal[cal] = new tsAdcLoadCal_t();
                    branchFineGain[cal] = new tsAdcGainCal_t();
                }
            }
        }

        public enum tsSampleFormat_t
        {
            Format8Bit = 0,
            Format12BitLSB,
            Format12BitMSB,
            Format14Bit
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

        //thunderscopeSampleInterruptRate

        [LibraryImport(library, EntryPoint = "thunderscopeDataEnable")]
        public static partial int DataEnable(nint ts, byte enable);

        [LibraryImport(library, EntryPoint = "thunderscopeRead")]
        public static unsafe partial int Read(nint ts, byte* buffer, uint len);

        [LibraryImport(library, EntryPoint = "thunderscopeReadCount")]
        public static unsafe partial int Read(nint ts, byte* buffer, uint len, out ulong count);

        [LibraryImport(library, EntryPoint = "thunderscopeFwUpdate")]
        public static unsafe partial int FirmwareUpdate(nint ts, byte* bitstream, uint len);

        [LibraryImport(library, EntryPoint = "thunderscopeGetFwProgress")]
        public static unsafe partial int FirmwareUpdateProgress(nint ts, out uint progress);

        [LibraryImport(library, EntryPoint = "thunderscopeUserDataRead")]
        public static unsafe partial int UserDataRead(nint ts, byte* buffer, uint offset, uint readLen);

        [LibraryImport(library, EntryPoint = "thunderscopeUserDataWrite")]
        public static unsafe partial int UserDataWrite(nint ts, byte* buffer, uint offset, uint writeLen);

        [DllImport(library, EntryPoint = "thunderscopeChanCalibrationSet")]     // Use runtime marshalling for now. Custom marshalling later.
        public static extern int SetAfeCalibration(nint ts, uint channel, in tsFrontendCalibration_t cal);

        [DllImport(library, EntryPoint = "thunderscopeAdcCalibrationGet")]      // Use runtime marshalling for now. Custom marshalling later.
        public static extern int GetAFECalibration(nint ts, uint channel, out tsFrontendCalibration_t cal);

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

        [LibraryImport(library, EntryPoint = "thunderscopeCalibrationManualAdcFineGain")]
        public static unsafe partial int SetAdcManualFineGain(nint ts, in byte[] ctrl);

        public enum tsEventType_t
        {
            TS_EVT_NONE = 0,
            TS_EVT_HOST_SW,
            TS_EVT_EXT_SYNC,
        }

        public struct tsEvent_t
        {
            public tsEventType_t type;
            public ulong index;
        }

        [LibraryImport(library, EntryPoint = "thunderscopeEventGet")]
        public static unsafe partial int GetEvent(nint ts, out tsEvent_t evt);

        public enum tsSyncMode_t
        {
            TS_SYNC_DISABLED = 0,
            TS_SYNC_OUT,
            TS_SYNC_IN
        }

        [LibraryImport(library, EntryPoint = "thunderscopeEventSyncAssert")]
        public static unsafe partial int AssertEventSync(nint ts);

        [LibraryImport(library, EntryPoint = "thunderscopeEventSyncPeriodicConfig")]
        public static unsafe partial int ConfigurePeriodicEventSync(nint ts, uint period_us);

        [LibraryImport(library, EntryPoint = "thunderscopeExtSyncConfig")]
        public static unsafe partial int ConfigureExtSync(nint ts, tsSyncMode_t mode);

        public enum tsRefClockMode_t
        {
            TS_REFCLK_NONE = 0,
            TS_REFCLK_OUT = 1,
            TS_REFCLK_IN = 2
        }

        [LibraryImport(library, EntryPoint = "thunderscopeRefClockSet")]
        public static unsafe partial int ConfigureRefClock(nint ts, tsRefClockMode_t mode, uint refclk_freq);

        // Factory methods
        [LibraryImport(library, EntryPoint = "thunderscopeFactoryProvisionPrepare")]
        public static unsafe partial int FactoryDataErase(nint ts, ulong dna);

        [LibraryImport(library, EntryPoint = "thunderscopeFactoryProvisionAppendTLV")]
        public static unsafe partial int FactoryDataAppend(nint ts, uint tag, uint length, byte* content);

        [LibraryImport(library, EntryPoint = "thunderscopeFactoryReadItem")]
        public static unsafe partial int FactoryDataRead(nint ts, uint tag, byte* content, uint maxLength);
    }
}
