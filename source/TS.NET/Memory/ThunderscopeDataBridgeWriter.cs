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
        private bool acquiringDataRegionFilledAndWaitingForReader = false;

        public Span<sbyte> AcquiringDataRegionI8 { get { return GetAcquiringDataRegionI8(); } }
        public ThunderscopeMonitoring Monitoring { get { return bridgeHeader.Monitoring; } }

        public unsafe ThunderscopeDataBridgeWriter(string bridgeNamespace, ThunderscopeBridgeConfig bridgeConfig)
        {
            string mmfName = bridgeNamespace + ".Bridge";
            var mmfCapacityBytes = (ulong)sizeof(ThunderscopeBridgeHeader) + sizeof(byte) + bridgeConfig.MaxAllDataRegionLengthBytes();
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
                    dataRegionBHeaderPointer = dataRegionADataPointer + bridgeConfig.MaxDataRegionLengthBytes();
                    dataRegionBDataPointer = dataRegionBHeaderPointer + sizeof(ThunderscopeBridgeDataRegionHeader);

                    bridgeHeader.Version = ThunderscopeBridgeHeader.BuildVersion;
                    bridgeHeader.Bridge = bridgeConfig;
                    bridgeHeader.AcquiringDataRegion = ThunderscopeMemoryAcquiringDataRegion.RegionA;
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
            if (acquiringDataRegionFilledAndWaitingForReader && dataRequestAndResponse > 0)
            {
                acquiringDataRegionFilledAndWaitingForReader = false;

                bridgeHeader.AcquiringDataRegion = bridgeHeader.AcquiringDataRegion switch      // Switch region
                {
                    ThunderscopeMemoryAcquiringDataRegion.RegionA => ThunderscopeMemoryAcquiringDataRegion.RegionB,
                    ThunderscopeMemoryAcquiringDataRegion.RegionB => ThunderscopeMemoryAcquiringDataRegion.RegionA,
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
        public void DataWritten(ThunderscopeHardwareConfig hardware, ThunderscopeProcessingConfig processing, bool triggered, ThunderscopeDataType dataType)
        {
            acquiringDataRegionFilledAndWaitingForReader = true;

            acquiringDataRegionHeader.Hardware = hardware;
            acquiringDataRegionHeader.Processing = processing;
            acquiringDataRegionHeader.Triggered = triggered;
            acquiringDataRegionHeader.DataType = dataType;
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

        private unsafe Span<sbyte> GetAcquiringDataRegionI8()
        {
            int maxDataRegionLength = (int)bridgeHeader.Bridge.MaxDataRegionLengthBytes();
            return bridgeHeader.AcquiringDataRegion switch
            {
                ThunderscopeMemoryAcquiringDataRegion.RegionA => new Span<sbyte>(dataRegionADataPointer, maxDataRegionLength),
                ThunderscopeMemoryAcquiringDataRegion.RegionB => new Span<sbyte>(dataRegionBDataPointer, maxDataRegionLength),
                _ => throw new InvalidDataException("Enum value not handled, add enum value to switch")
            };
        }

        private unsafe void SetAcquiringRegionHeader(ref ThunderscopeBridgeDataRegionHeader header)
        {
            switch (bridgeHeader.AcquiringDataRegion)
            {
                case ThunderscopeMemoryAcquiringDataRegion.RegionA:
                    unsafe { Unsafe.Copy(dataRegionAHeaderPointer, ref header); }
                    break;
                case ThunderscopeMemoryAcquiringDataRegion.RegionB:
                    unsafe { Unsafe.Copy(dataRegionBHeaderPointer, ref header); }
                    break;
                default:
                    throw new InvalidDataException("Enum value not handled, add enum value to switch");
            }
        }
    }
}
