using System.Runtime.InteropServices;

namespace TS.NET.Driver.LiteX
{
    internal static partial class Interop
    {
        private const string library = "liblitex";
        
        [StructLayout(LayoutKind.Sequential)]
        public struct tsChannelParam_t
        {
            uint volt_scale_mV;     /**< Set full scale voltage in millivolts */
            int volt_offset_mV;     /**< Set offset voltage in millivolts */
            uint bandwidth;         /**< Set Bandwidth Filter in MHz. Next highest filter will be selected */
            byte coupling;          /**< Select AD/DC coupling for channel.  Use tsChannelCoupling_t enum */
            byte term;              /**< Select Termination mode for channel.  Use tsChannelTerm_t enum */
            byte active;            /**< Active flag for the channel. 1 to enable, 0 to disable */
            byte reserved;          /**< Reserved byte for 32-bit alignment*/
        }

        [LibraryImport(library, EntryPoint = "thunderscopeOpen")]
        public static partial nint Open(uint devIndex);

        [LibraryImport(library, EntryPoint = "thunderscopeClose")]
        public static partial int Close(nint ts);

        [LibraryImport(library, EntryPoint = "thunderscopeChannelConfigGet")]
        public static partial int GetChannelConfig(nint ts, uint channel, ref tsChannelParam_t conf);
    }
}