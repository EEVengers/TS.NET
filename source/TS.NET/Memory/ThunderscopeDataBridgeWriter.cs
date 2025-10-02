//using System.IO.MemoryMappedFiles;
//using System.Runtime.CompilerServices;
//using System.Runtime.InteropServices;
//using TS.NET.Memory.Unix;
//using TS.NET.Memory.Windows;

//namespace TS.NET
//{
//    // This is a lock-free memory-mapped interprocess/cross-platform bridge between producer & consumer, with only a single writer and a single reader
//    public class ThunderscopeDataBridgeWriter : IDisposable
//    {
//        private readonly IMemoryFile bridgeFile;
//        private readonly MemoryMappedViewAccessor bridgeView;
//        private readonly unsafe byte* bridgeBasePointer;
//        private readonly unsafe byte* dataRequestAndResponsePointer;
//        private readonly unsafe byte* dataPointer;
//        private ThunderscopeBridgeHeader bridgeHeader;
//        private byte dataRequestAndResponse;
//        private bool acquiringDataRegionFilledAndWaitingForReader = false;

//        public ThunderscopeMonitoring Monitoring { get { return bridgeHeader.Monitoring; } }

//        public unsafe ThunderscopeDataBridgeWriter(string thunderscopeNamespace, ulong maxDataLengthBytes)
//        {
//            string mmfName = thunderscopeNamespace + ".Bridge";
//            var mmfCapacityBytes = (ulong)sizeof(ThunderscopeBridgeHeader) + sizeof(byte) + maxDataLengthBytes;
//            bridgeFile = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new MemoryFileWindows(mmfName, mmfCapacityBytes) : new MemoryFileUnix(mmfName, mmfCapacityBytes);
//            try
//            {
//                bridgeView = bridgeFile.MappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
//                try
//                {
//                    bridgeBasePointer = GetPointer();
//                    dataRequestAndResponsePointer = bridgeBasePointer + sizeof(ThunderscopeBridgeHeader);
//                    dataPointer = dataRequestAndResponsePointer + sizeof(byte);

//                    bridgeHeader.Version = ThunderscopeBridgeHeader.BuildVersion;
//                    bridgeHeader.MaxDataLengthBytes = maxDataLengthBytes;

//                    SetBridgeHeader();
//                }
//                catch
//                {
//                    bridgeView.Dispose();
//                    throw;
//                }
//            }
//            catch
//            {
//                bridgeFile.Dispose();
//                throw;
//            }
//        }

//        public void Dispose()
//        {
//            bridgeView.SafeMemoryMappedViewHandle.ReleasePointer();
//            bridgeView.Flush();
//            bridgeView.Dispose();
//            bridgeFile.Dispose();
//        }

//        public ThunderscopeHardwareConfig Hardware
//        {
//            set
//            {
//                // This is a shallow copy, but considering the struct should be 100% blitable (i.e. no reference types), this is effectively a full copy
//                bridgeHeader.Hardware = value;
//                SetBridgeHeader();
//            }
//        }

//        public ThunderscopeProcessingConfig Processing
//        {
//            set
//            {
//                // This is a shallow copy, but considering the struct should be 100% blitable (i.e. no reference types), this is effectively a full copy
//                bridgeHeader.Processing = value;
//                SetBridgeHeader();
//            }
//        }

//        public void HandleReader()
//        {
//            GetDataRequestAndResponse();
//            if (acquiringDataRegionFilledAndWaitingForReader && dataRequestAndResponse > 0)
//            {
//                acquiringDataRegionFilledAndWaitingForReader = false;
//                SetBridgeHeader();

//                dataRequestAndResponse = 0;    // Reader will see this change and use the new region
//                SetDataRequestAndResponse();                            
//            }
//        }

//        private void GetDataRequestAndResponse()
//        {
//            unsafe { Unsafe.Copy(ref dataRequestAndResponse, dataRequestAndResponsePointer); }
//        }

//        private void SetDataRequestAndResponse()
//        {
//            unsafe { Unsafe.Copy(dataRequestAndResponsePointer, ref dataRequestAndResponse); }
//        }

//        private void SetBridgeHeader()
//        {
//            unsafe { Unsafe.Copy(bridgeBasePointer, ref bridgeHeader); }
//        }

//        private unsafe byte* GetPointer()
//        {
//            byte* ptr = null;
//            bridgeView.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
//            if (ptr == null)
//                throw new InvalidOperationException("Failed to acquire a pointer to the memory mapped file view.");

//            return ptr;
//        }
//    }
//}
