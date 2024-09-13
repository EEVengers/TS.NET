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
        private bool IsHeaderSet { get { GetBridgeHeader(); return bridgeHeader.Version != 0; } }
        private byte dataRequestAndResponse;

        public ReadOnlySpan<sbyte> AcquiredRegionI8 { get { return GetAcquiredRegionI8(); } }
        public ReadOnlySpan<byte> AcquiredRegionU8 { get { return GetAcquiredRegionU8(); } }        // Useful for the Socket API which only accepts byte

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
                    while (!IsHeaderSet)
                    {
                        Console.WriteLine("Waiting for Thunderscope data bridge writer to set header...");
                        Thread.Sleep(1000);
                    }
                    GetBridgeHeader();

                    dataRequestAndResponsePointer = bridgeBasePointer + sizeof(ThunderscopeBridgeHeader);
                    dataRegionAHeaderPointer = dataRequestAndResponsePointer + sizeof(byte);
                    dataRegionADataPointer = dataRegionAHeaderPointer + sizeof(ThunderscopeBridgeDataRegionHeader);
                    dataRegionBHeaderPointer = dataRegionADataPointer + bridgeHeader.Bridge.DataRegionCapacityBytes();
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

        public ThunderscopeHardwareConfig Hardware
        {
            get
            {
                GetBridgeHeader();
                return bridgeHeader.Hardware;
            }
        }

        public ThunderscopeProcessingConfig Processing
        {
            get
            {
                GetBridgeHeader();
                return bridgeHeader.Processing;
            }
        }

        public ThunderscopeMonitoring Monitoring
        {
            get
            {
                GetBridgeHeader();
                return bridgeHeader.Monitoring;
            }
        }

        public bool RequestAndWaitForData(int millisecondsTimeout)
        {
            dataRequestAndResponse = 1;
            SetDataRequestAndResponse();
            // Change this later to either a better tight loop, or a interprocess sync primitive
            for (int i = 0; i < millisecondsTimeout; i++)
            {
                GetDataRequestAndResponse();
                if (dataRequestAndResponse == 0) // Data has arrived!
                {
                    return true;
                }
                Thread.Sleep(1);
            }
            return false;
        }

        private void GetDataRequestAndResponse()
        {
            unsafe { Unsafe.Copy(ref dataRequestAndResponse, dataRequestAndResponsePointer); }
        }

        private void SetDataRequestAndResponse()
        {
            unsafe { Unsafe.Copy(dataRequestAndResponsePointer, ref dataRequestAndResponse); }
        }

        private void GetBridgeHeader()
        {
            unsafe { Unsafe.Copy(ref bridgeHeader, bridgeBasePointer); }
        }

        //private void SetHeader()
        //{
        //    unsafe { Unsafe.Copy(basePointer, ref header); }
        //}

        private unsafe byte* GetPointer()
        {
            byte* ptr = null;
            bridgeView.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            if (ptr == null)
                throw new InvalidOperationException("Failed to acquire a pointer to the memory mapped file view.");

            return ptr;
        }

        ThunderscopeBridgeDataRegionHeader acquiredDataRegionHeader;
        private unsafe ReadOnlySpan<sbyte> GetAcquiredRegionI8()
        {
            GetAcquiredRegionHeader(ref acquiredDataRegionHeader);
            int currentRegionLength = (int)(acquiredDataRegionHeader.Processing.CurrentChannelCount * acquiredDataRegionHeader.Processing.CurrentChannelDataLength * bridgeHeader.Bridge.ChannelDataType.Width());
            return bridgeHeader.AcquiringRegion switch
            {
                ThunderscopeMemoryAcquiringRegion.RegionA => new ReadOnlySpan<sbyte>(dataRegionBDataPointer, currentRegionLength), // If acquiring region is Region A, return Region B
                ThunderscopeMemoryAcquiringRegion.RegionB => new ReadOnlySpan<sbyte>(dataRegionADataPointer, currentRegionLength), // If acquiring region is Region B, return Region A
                _ => throw new InvalidDataException("Enum value not handled, add enum value to switch")
            };
        }

        private unsafe ReadOnlySpan<byte> GetAcquiredRegionU8()
        {
            GetAcquiredRegionHeader(ref acquiredDataRegionHeader);
            int currentRegionLength = (int)(acquiredDataRegionHeader.Processing.CurrentChannelCount * acquiredDataRegionHeader.Processing.CurrentChannelDataLength * bridgeHeader.Bridge.ChannelDataType.Width());
            return bridgeHeader.AcquiringRegion switch
            {
                ThunderscopeMemoryAcquiringRegion.RegionA => new ReadOnlySpan<byte>(dataRegionBDataPointer, currentRegionLength),  // If acquiring region is Region A, return Region B
                ThunderscopeMemoryAcquiringRegion.RegionB => new ReadOnlySpan<byte>(dataRegionADataPointer, currentRegionLength),  // If acquiring region is Region B, return Region A
                _ => throw new InvalidDataException("Enum value not handled, add enum value to switch")
            };
        }

        public unsafe void GetAcquiredRegionHeader(ref ThunderscopeBridgeDataRegionHeader header)
        {
            switch (bridgeHeader.AcquiringRegion)
            {
                case ThunderscopeMemoryAcquiringRegion.RegionA:
                    unsafe { Unsafe.Copy(ref header, dataRegionBHeaderPointer); }
                    break;
                case ThunderscopeMemoryAcquiringRegion.RegionB:
                    unsafe { Unsafe.Copy(ref header, dataRegionAHeaderPointer); }
                    break;
                default:
                    throw new InvalidDataException("Enum value not handled, add enum value to switch");
            }
        }

        // For use by TS.NET.Native.BridgeReader only
        public unsafe byte* GetAcquiredRegionPointer()
        {
            return bridgeHeader.AcquiringRegion switch
            {
                ThunderscopeMemoryAcquiringRegion.RegionA => dataRegionBDataPointer,    // If acquiring region is Region A, return Region B
                ThunderscopeMemoryAcquiringRegion.RegionB => dataRegionADataPointer,    // If acquiring region is Region B, return Region A
                _ => throw new InvalidDataException("Enum value not handled, add enum value to switch")
            };
        }
    }
}