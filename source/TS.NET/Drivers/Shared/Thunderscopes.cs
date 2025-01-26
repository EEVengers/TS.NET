using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TS.NET.Driver.Shared.Windows;

namespace TS.NET.Driver.Shared
{
    public static class Thunderscopes
    {
        private const int INVALID_HANDLE_VALUE = -1;
        private const int ERROR_INSUFFICIENT_BUFFER = 122;
        private static IntPtr NULL = IntPtr.Zero;

        public static List<ThunderscopeDevice> List(string hardwareDriver)
        {
            Guid classGuid = Guid.Empty;

            switch (hardwareDriver.ToLower())
            {
                case "xdma":
                    classGuid = Guid.Parse("{74c7e4a9-6d5d-4a70-bc0d-20691dff9e9d}");
                    break;
                case "litex":
                    //classGuid = Guid.Parse("{164adc02-e1ae-4fe1-a904-9a013577b891}");
                    classGuid = Guid.Parse("{dac3fa32-b912-4302-a1e7-c37299053dac}");
                    break;
                default:
                    throw new NotImplementedException();
            }
            List<ThunderscopeDevice> devices = new();

            if (OperatingSystem.IsWindows())
            {
                var deviceInfo = Windows.Interop.SetupDiGetClassDevs(ref classGuid, NULL, NULL, DiGetClassFlags.DIGCF_PRESENT | DiGetClassFlags.DIGCF_DEVICEINTERFACE);
                if ((IntPtr.Size == 4 && deviceInfo.ToInt32() == INVALID_HANDLE_VALUE) || (IntPtr.Size == 8 && deviceInfo.ToInt64() == INVALID_HANDLE_VALUE))
                    throw new Exception("SetupDiGetClassDevs - failed with INVALID_HANDLE_VALUE");

                SP_DEVICE_INTERFACE_DATA deviceInterface = new();
                deviceInterface.CbSize = Marshal.SizeOf(deviceInterface);      //32
                for (uint i = 0; Interop.SetupDiEnumDeviceInterfaces(deviceInfo, NULL, ref classGuid, i, ref deviceInterface); ++i)        //Marshal.GetLastSystemError() == ERROR_NO_MORE_ITEMS
                {
                    uint detailLength = 0;
                    if (!Interop.SetupDiGetDeviceInterfaceDetail(deviceInfo, ref deviceInterface, NULL, 0, ref detailLength, NULL) && Marshal.GetLastSystemError() != ERROR_INSUFFICIENT_BUFFER)
                        throw new Exception("SetupDiGetDeviceInterfaceDetail - failed getting length with ERROR_INSUFFICIENT_BUFFER");
                    if (detailLength > 255)
                        throw new Exception("SetupDiGetDeviceInterfaceDetail - failed getting length by returning a length greater than 255 which won't fit into fixed length string field");

                    SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetail = new();
                    deviceInterfaceDetail.CbSize = IntPtr.Size == 8 ? 8 : 6;            // 6 bytes for x86, 8 bytes for x64
                                                                                        // Could use Marshal.AllocHGlobal and Marshal.FreeHGlobal, inside Try/Finally, but might as well use the Marshalling syntax sugar
                    if (!Interop.SetupDiGetDeviceInterfaceDetail(deviceInfo, ref deviceInterface, ref deviceInterfaceDetail, detailLength, NULL, NULL))
                        throw new Exception("SetupDiGetDeviceInterfaceDetail - failed");

                    devices.Add(new ThunderscopeDevice(DevicePath: deviceInterfaceDetail.DevicePath));
                }
            }
            if (OperatingSystem.IsLinux())
            {
                throw new NotImplementedException();
            }
            if (OperatingSystem.IsMacOS())
            {
                throw new NotImplementedException();
            }

            return devices;
        }
    }
}
