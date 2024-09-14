using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using TS.NET.Memory.Unix;
using TS.NET.Memory.Windows;

namespace TS.NET
{
    // This is a shared memory-mapped file between processes, with only a single writer and a single reader with a header struct
    public class ThunderscopeDataBridgeReader : IDisposable
    {
        private readonly IMemoryFile bridgeFile;
        private readonly MemoryMappedViewAccessor bridgeView;
        private readonly unsafe byte* bridgeBasePointer;
        private readonly unsafe byte* dataRequestAndResponsePointer;
        private readonly unsafe byte* dataRegionAHeaderPointer;
        private readonly unsafe byte* dataRegionADataPointer;
        private readonly unsafe byte* dataRegionBHeaderPointer;
        private readonly unsafe byte* dataRegionBDataPointer;
        private ThunderscopeBridgeHeader bridgeHeader;

        public ReadOnlySpan<sbyte> AcquiredDataRegionI8 { get { GetBridgeHeader(); return GetAcquiredDataRegionI8(); } }
        public ReadOnlySpan<byte> AcquiredDataRegionU8 { get { GetBridgeHeader(); return GetAcquiredDataRegionU8(); } }        // Useful for the Socket API which only accepts byte
        public ThunderscopeBridgeDataRegionHeader AcquiredDataRegionHeader { get { return GetAcquiredDataRegionHeader(); } }
        public ThunderscopeHardwareConfig Hardware { get { GetBridgeHeader(); return bridgeHeader.Hardware; } }
        public ThunderscopeProcessingConfig Processing { get { GetBridgeHeader(); return bridgeHeader.Processing; } }
        public ThunderscopeMonitoring Monitoring { get { GetBridgeHeader(); return bridgeHeader.Monitoring; } }

        public unsafe ThunderscopeDataBridgeReader(string bridgeNamespace)
        {
            string mmfName = bridgeNamespace + ".Bridge";
            if (OperatingSystem.IsWindows())
            {
                while (!MemoryFileWindows.Exists(mmfName))
                {
                    Console.WriteLine("Waiting for Thunderscope data bridge writer to create MMF...");
                    Thread.Sleep(1000);
                }

                bridgeFile = new MemoryFileWindows(mmfName);
            }
            else
            {
                while (!MemoryFileUnix.Exists(mmfName))
                {
                    Console.WriteLine("Waiting for Thunderscope data bridge writer to create MMF...");
                    Thread.Sleep(1000);
                }

                bridgeFile = new MemoryFileUnix(mmfName, MemoryFileUnix.Size(mmfName));
            }

            try
            {
                bridgeView = bridgeFile.MappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);

                try
                {
                    bridgeBasePointer = GetPointer();
                    while (GetBridgeVersion() == 0)     // Assumption that a newly created MMF will be filled with zeros
                    {
                        Console.WriteLine("Waiting for Thunderscope data bridge writer to set header...");
                        Thread.Sleep(1000);
                    }
                    if (GetBridgeVersion() != ThunderscopeBridgeHeader.BuildVersion)
                    {
                        throw new Exception("Bridge writer/reader version mismatch");
                    }
                    GetBridgeHeader();      // Get initial copy of data, mainly for debugging
                    dataRequestAndResponsePointer = bridgeBasePointer + sizeof(ThunderscopeBridgeHeader);
                    dataRegionAHeaderPointer = dataRequestAndResponsePointer + sizeof(byte);
                    dataRegionADataPointer = dataRegionAHeaderPointer + sizeof(ThunderscopeBridgeDataRegionHeader);
                    dataRegionBHeaderPointer = dataRegionADataPointer + bridgeHeader.Bridge.MaxDataRegionLengthBytes();
                    dataRegionBDataPointer = dataRegionBHeaderPointer + sizeof(ThunderscopeBridgeDataRegionHeader);
                }
                catch
                {
                    bridgeView.Dispose();
                    throw;
                }
            }
            catch
            {
                bridgeFile.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            bridgeView.SafeMemoryMappedViewHandle.ReleasePointer();
            bridgeView.Flush();
            bridgeView.Dispose();
            bridgeFile.Dispose();
        }

        /// <summary>
        /// Used at startup of consumer to allow the use of data already on the bridge
        /// </summary>
        public bool StartupDataExists()
        {
            return Monitoring.Processing.BridgeWrites > 0;
        }

        /// <summary>
        /// Used by consumer to request new data
        /// </summary>
        public bool RequestAndWaitForData(int millisecondsTimeout)
        {
            SetDataRequestAndResponse(1);
            // Change this later to either a better tight loop, or a interprocess sync primitive
            for (int i = 0; i < millisecondsTimeout; i++)
            {
                var dataRequestAndResponse = GetDataRequestAndResponse();
                if (dataRequestAndResponse == 0) // Data has arrived!
                {
                    return true;
                }
                Thread.Sleep(1);
            }
            return false;
        }

        private byte GetDataRequestAndResponse()
        {
            byte dataRequestAndResponse = 0;
            unsafe { Unsafe.Copy(ref dataRequestAndResponse, dataRequestAndResponsePointer); }
            return dataRequestAndResponse;
        }

        private void SetDataRequestAndResponse(byte dataRequestAndResponse)
        {
            unsafe { Unsafe.Copy(dataRequestAndResponsePointer, ref dataRequestAndResponse); }
        }

        private void GetBridgeHeader()
        {
            unsafe { Unsafe.Copy(ref bridgeHeader, bridgeBasePointer); }
        }

        private uint GetBridgeVersion()
        {
            uint version = 0;
            unsafe { Unsafe.Copy(ref version, bridgeBasePointer); }
            return version;
        }

        private ThunderscopeBridgeDataRegionHeader GetAcquiredDataRegionHeader()
        {
            var acquiredDataRegionHeader = new ThunderscopeBridgeDataRegionHeader();
            unsafe
            {
                switch (bridgeHeader.AcquiringDataRegion)
                {
                    case ThunderscopeMemoryAcquiringDataRegion.RegionA:
                        Unsafe.Copy(ref acquiredDataRegionHeader, dataRegionBHeaderPointer);
                        break;
                    case ThunderscopeMemoryAcquiringDataRegion.RegionB:
                        Unsafe.Copy(ref acquiredDataRegionHeader, dataRegionAHeaderPointer);
                        break;
                    default:
                        throw new InvalidDataException("Enum value not handled, add enum value to switch");
                }
            }
            return acquiredDataRegionHeader;
        }

        private unsafe byte* GetPointer()
        {
            byte* ptr = null;
            bridgeView.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            if (ptr == null)
                throw new InvalidOperationException("Failed to acquire a pointer to the memory mapped file view.");

            return ptr;
        }

        private ReadOnlySpan<sbyte> GetAcquiredDataRegionI8()
        {
            var acquiredDataRegionHeader = GetAcquiredDataRegionHeader();
            unsafe
            {
                return bridgeHeader.AcquiringDataRegion switch
                {
                    ThunderscopeMemoryAcquiringDataRegion.RegionA => new ReadOnlySpan<sbyte>(dataRegionBDataPointer, acquiredDataRegionHeader.DataRegionDataLengthBytes()), // If acquiring region is Region A, return Region B
                    ThunderscopeMemoryAcquiringDataRegion.RegionB => new ReadOnlySpan<sbyte>(dataRegionADataPointer, acquiredDataRegionHeader.DataRegionDataLengthBytes()), // If acquiring region is Region B, return Region A
                    _ => throw new InvalidDataException("Enum value not handled, add enum value to switch")
                };
            }
        }

        private ReadOnlySpan<byte> GetAcquiredDataRegionU8()
        {
            var acquiredDataRegionHeader = GetAcquiredDataRegionHeader();
            unsafe
            {
                return bridgeHeader.AcquiringDataRegion switch
                {
                    ThunderscopeMemoryAcquiringDataRegion.RegionA => new ReadOnlySpan<byte>(dataRegionBDataPointer, acquiredDataRegionHeader.DataRegionDataLengthBytes()),  // If acquiring region is Region A, return Region B
                    ThunderscopeMemoryAcquiringDataRegion.RegionB => new ReadOnlySpan<byte>(dataRegionADataPointer, acquiredDataRegionHeader.DataRegionDataLengthBytes()),  // If acquiring region is Region B, return Region A
                    _ => throw new InvalidDataException("Enum value not handled, add enum value to switch")
                };
            }
        }

        // For use by TS.NET.Native.BridgeReader only
        public IntPtr GetAcquiredDataRegionHeaderPointer()
        {
            unsafe
            {
                return bridgeHeader.AcquiringDataRegion switch
                {
                    ThunderscopeMemoryAcquiringDataRegion.RegionA => (IntPtr)dataRegionBHeaderPointer,    // If acquiring region is Region A, return Region B
                    ThunderscopeMemoryAcquiringDataRegion.RegionB => (IntPtr)dataRegionAHeaderPointer,    // If acquiring region is Region B, return Region A
                    _ => throw new InvalidDataException("Enum value not handled, add enum value to switch")
                };
            }
        }

        public int GetAcquiredDataRegionDataLength()
        {
            var acquiredRegionHeader = GetAcquiredDataRegionHeader();
            return acquiredRegionHeader.DataRegionDataLengthBytes();
        }

        public IntPtr GetAcquiredDataRegionDataPointer()
        {
            unsafe
            {
                return bridgeHeader.AcquiringDataRegion switch
                {
                    ThunderscopeMemoryAcquiringDataRegion.RegionA => (IntPtr)dataRegionBDataPointer,    // If acquiring region is Region A, return Region B
                    ThunderscopeMemoryAcquiringDataRegion.RegionB => (IntPtr)dataRegionADataPointer,    // If acquiring region is Region B, return Region A
                    _ => throw new InvalidDataException("Enum value not handled, add enum value to switch")
                };
            }
        }
    }
}