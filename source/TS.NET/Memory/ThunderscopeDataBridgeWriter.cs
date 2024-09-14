using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TS.NET.Memory.Unix;
using TS.NET.Memory.Windows;

namespace TS.NET
{
    // This is a lock-free memory-mapped interprocess/cross-platform bridge between producer & consumer, with only a single writer and a single reader
    public class ThunderscopeDataBridgeWriter : IDisposable
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
        private byte dataRequestAndResponse;
        private bool acquiringRegionFilledAndWaitingForReader = false;

        public Span<sbyte> AcquiringRegionI8 { get { return GetAcquiringRegionI8(); } }
        public ThunderscopeMonitoring Monitoring { get { return bridgeHeader.Monitoring; } }

        public unsafe ThunderscopeDataBridgeWriter(string bridgeNamespace, ThunderscopeBridgeConfig bridgeConfig)
        {
            string mmfName = bridgeNamespace + ".Bridge";
            var mmfCapacityBytes = (ulong)sizeof(ThunderscopeBridgeHeader) + sizeof(byte) + bridgeConfig.DataTotalCapacityBytes();
            bridgeFile = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? new MemoryFileWindows(mmfName, mmfCapacityBytes) : new MemoryFileUnix(mmfName, mmfCapacityBytes);
            try
            {
                bridgeView = bridgeFile.MappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
                try
                {
                    bridgeBasePointer = GetPointer();
                    dataRequestAndResponsePointer = bridgeBasePointer + sizeof(ThunderscopeBridgeHeader);
                    dataRegionAHeaderPointer = dataRequestAndResponsePointer + sizeof(byte);
                    dataRegionADataPointer = dataRegionAHeaderPointer + sizeof(ThunderscopeBridgeDataRegionHeader);
                    dataRegionBHeaderPointer = dataRegionADataPointer + bridgeConfig.DataRegionCapacityBytes();
                    dataRegionBDataPointer = dataRegionBHeaderPointer + sizeof(ThunderscopeBridgeDataRegionHeader);

                    bridgeHeader.Version = ThunderscopeBridgeHeader.BuildVersion;
                    bridgeHeader.Bridge = bridgeConfig;
                    bridgeHeader.AcquiringRegion = ThunderscopeMemoryAcquiringRegion.RegionA;
                    SetBridgeHeader();
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
            set
            {
                // This is a shallow copy, but considering the struct should be 100% blitable (i.e. no reference types), this is effectively a full copy
                bridgeHeader.Hardware = value;
                SetBridgeHeader();
            }
        }

        public ThunderscopeProcessingConfig Processing
        {
            set
            {
                // This is a shallow copy, but considering the struct should be 100% blitable (i.e. no reference types), this is effectively a full copy
                bridgeHeader.Processing = value;
                SetBridgeHeader();
            }
        }

        public void MonitoringReset()
        {
            bridgeHeader.Monitoring.Processing.BridgeWrites = 0;
            bridgeHeader.Monitoring.Processing.BridgeReads = 0;
            bridgeHeader.Monitoring.Processing.BridgeWritesPerSec = 0;
            SetBridgeHeader();
        }

        private ulong monitoringIntervalTotalAcquisitions = 0;
        private DateTimeOffset monitoringIntervalStart = DateTimeOffset.UtcNow;
        public void HandleReader()
        {
            GetDataRequestAndResponse();
            if (acquiringRegionFilledAndWaitingForReader && dataRequestAndResponse > 0)
            {
                acquiringRegionFilledAndWaitingForReader = false;

                bridgeHeader.AcquiringRegion = bridgeHeader.AcquiringRegion switch      // Switch region
                {
                    ThunderscopeMemoryAcquiringRegion.RegionA => ThunderscopeMemoryAcquiringRegion.RegionB,
                    ThunderscopeMemoryAcquiringRegion.RegionB => ThunderscopeMemoryAcquiringRegion.RegionA,
                    _ => throw new InvalidDataException("Enum value not handled, add enum value to switch")
                };
                bridgeHeader.Monitoring.Processing.BridgeReads++;
                SetBridgeHeader();

                dataRequestAndResponse = 0;    // Reader will see this change and use the new region
                SetDataRequestAndResponse();                            
            }

            var intervalTimeElapsed = DateTimeOffset.UtcNow.Subtract(monitoringIntervalStart).TotalSeconds;
            if (intervalTimeElapsed > 1)
            {
                var intervalTotalAcquisitions = bridgeHeader.Monitoring.Processing.BridgeWrites - monitoringIntervalTotalAcquisitions;
                bridgeHeader.Monitoring.Processing.BridgeWritesPerSec = (float)(intervalTotalAcquisitions / intervalTimeElapsed);
                monitoringIntervalTotalAcquisitions = bridgeHeader.Monitoring.Processing.BridgeWrites;
                monitoringIntervalStart = monitoringIntervalStart = DateTimeOffset.UtcNow;
            }
        }

        ThunderscopeBridgeDataRegionHeader acquiringDataRegionHeader;
        public void DataWritten(ThunderscopeHardwareConfig hardware, ThunderscopeProcessingConfig processing, bool triggered)
        {
            acquiringRegionFilledAndWaitingForReader = true;

            acquiringDataRegionHeader.Hardware = hardware;
            acquiringDataRegionHeader.Processing = processing;
            acquiringDataRegionHeader.Triggered = triggered;
            SetAcquiringRegionHeader(ref acquiringDataRegionHeader);

            bridgeHeader.Monitoring.Processing.BridgeWrites++;
            SetBridgeHeader();            
        }

        private void GetDataRequestAndResponse()
        {
            unsafe { Unsafe.Copy(ref dataRequestAndResponse, dataRequestAndResponsePointer); }
        }

        private void SetDataRequestAndResponse()
        {
            unsafe { Unsafe.Copy(dataRequestAndResponsePointer, ref dataRequestAndResponse); }
        }

        private void SetBridgeHeader()
        {
            unsafe { Unsafe.Copy(bridgeBasePointer, ref bridgeHeader); }
        }

        private unsafe byte* GetPointer()
        {
            byte* ptr = null;
            bridgeView.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            if (ptr == null)
                throw new InvalidOperationException("Failed to acquire a pointer to the memory mapped file view.");

            return ptr;
        }

        private unsafe Span<sbyte> GetAcquiringRegionI8()
        {
            int maxRegionLength = (int)bridgeHeader.Bridge.DataRegionCapacityBytes();
            return bridgeHeader.AcquiringRegion switch
            {
                ThunderscopeMemoryAcquiringRegion.RegionA => new Span<sbyte>(dataRegionADataPointer, maxRegionLength),
                ThunderscopeMemoryAcquiringRegion.RegionB => new Span<sbyte>(dataRegionBDataPointer, maxRegionLength),
                _ => throw new InvalidDataException("Enum value not handled, add enum value to switch")
            };
        }

        private unsafe void SetAcquiringRegionHeader(ref ThunderscopeBridgeDataRegionHeader header)
        {
            switch (bridgeHeader.AcquiringRegion)
            {
                case ThunderscopeMemoryAcquiringRegion.RegionA:
                    unsafe { Unsafe.Copy(dataRegionAHeaderPointer, ref header); }
                    break;
                case ThunderscopeMemoryAcquiringRegion.RegionB:
                    unsafe { Unsafe.Copy(dataRegionBHeaderPointer, ref header); }
                    break;
                default:
                    throw new InvalidDataException("Enum value not handled, add enum value to switch");
            }
        }
    }
}
