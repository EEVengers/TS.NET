using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TS.NET.Native.BridgeReader
{
    public class UnmanagedMethods
    {
        /// <summary>
        /// Open reference to thunderscope bridge
        /// </summary>
        /// <returns>Success: bridge reader pointer, fail: -1 (possible cause: bridge version mismatch)</returns>
        [UnmanagedCallersOnly(EntryPoint = "open")]
        public static IntPtr Open()
        {
            try
            {
                ThunderscopeDataBridgeReader bridgeReader = new("ThunderScope.0");
                GCHandle handle = GCHandle.Alloc(bridgeReader, GCHandleType.Pinned);
                return handle.AddrOfPinnedObject();
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Close reference to thunderscope bridge
        /// </summary>
        [UnmanagedCallersOnly(EntryPoint = "close")]
        public static void Close(IntPtr bridgeReaderPtr)
        {
            var handle = GCHandle.FromIntPtr(bridgeReaderPtr);
            if (handle.Target != null)
            {
                var bridgeReader = (ThunderscopeDataBridgeReader)handle.Target;
                bridgeReader.Dispose();
            }
            handle.Free();   // Free the managed object
        }

        /// <summary>
        /// Request and wait for new data, with millisecond timeout
        /// </summary>
        /// <returns>Success, new data available: 0, invalid bridge pointer: -1, timeout: 1</returns>
        [UnmanagedCallersOnly(EntryPoint = "requestAndWaitForData")]
        public static int RequestAndWaitForData(IntPtr bridgeReaderPtr, int millisecondsTimeout)
        {
            var handle = GCHandle.FromIntPtr(bridgeReaderPtr);
            if (handle.Target != null)
            {
                var bridgeReader = (ThunderscopeDataBridgeReader)handle.Target;
                return bridgeReader.RequestAndWaitForData(millisecondsTimeout) ? 0 : 1;
            }
            return -1;
        }

        /// <summary>
        /// Copy data header to buffer and return length of header.
        /// </summary>
        /// <returns>Success: length of header in buffer, invalid bridge pointer: -1, buffer too short: -2</returns>
        [UnmanagedCallersOnly(EntryPoint = "getDataHeader")]
        public static unsafe int GetDataHeader(IntPtr bridgeReaderPtr, IntPtr buffer, int maxLength)
        {
            var handle = GCHandle.FromIntPtr(bridgeReaderPtr);
            if (handle.Target != null)
            {
                var bridgeReader = (ThunderscopeDataBridgeReader)handle.Target;
                var headerSize = sizeof(ThunderscopeBridgeDataRegionHeader);
                if (headerSize > maxLength)
                {
                    return -2;
                }
                var headerPointer = bridgeReader.GetAcquiredRegionHeaderPointer();
                Unsafe.CopyBlock((byte*)buffer, (byte*)headerPointer, (uint)headerSize);
                return headerSize;
            }
            return -1;
        }

        /// <summary>
        /// Copy data to buffer and return length of data
        /// </summary>
        /// <returns>Success: length of data in buffer, invalid bridge pointer: -1, buffer too short: -2</returns>
        [UnmanagedCallersOnly(EntryPoint = "getData")]
        public static unsafe int GetData(IntPtr bridgeReaderPtr, IntPtr buffer, int maxLength)
        {
            var handle = GCHandle.FromIntPtr(bridgeReaderPtr);
            if (handle.Target != null)
            {
                var bridgeReader = (ThunderscopeDataBridgeReader)handle.Target;
                var dataSize = bridgeReader.GetAcquiredRegionDataLength();
                if (dataSize > maxLength)
                {
                    return -2;
                }
                var dataPointer = bridgeReader.GetAcquiredRegionDataPointer();
                Unsafe.CopyBlock((byte*)buffer, (byte*)dataPointer, (uint)dataSize);
                return dataSize;
            }
            return -1;
        }
    }
}