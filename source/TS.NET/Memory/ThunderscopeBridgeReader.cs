using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TS.NET.Memory.Unix;
using TS.NET.Memory.Windows;

namespace TS.NET
{
    // This is a shared memory-mapped file between processes, with only a single writer and a single reader with a header struct
    public class ThunderscopeBridgeReader : IDisposable
    {
        private readonly IMemoryFile file;
        private readonly MemoryMappedViewAccessor view;
        private unsafe byte* basePointer;
        private unsafe byte* dataPointer { get; }
        private ThunderscopeBridgeHeader header;
        private bool IsHeaderSet { get { GetHeader(); return header.Version != 0; } }
        private readonly IInterprocessSemaphoreReleaser dataRequestSemaphore;           // When data is desired, this is signalled to the engine to gather data.
        private readonly IInterprocessSemaphoreWaiter dataResponseSemaphore;            // When this is signalled, data is ready to be consumed.
        private bool hasSignaledRequest = false;

        public ReadOnlySpan<sbyte> AcquiredRegionI8 { get { return GetAcquiredRegionI8(); } }
        public ReadOnlySpan<byte> AcquiredRegionU8 { get { return GetAcquiredRegionU8(); } }        // Useful for the Socket API which only accepts byte

        public unsafe ThunderscopeBridgeReader(string memoryName)
        {
            if (OperatingSystem.IsWindows())
            {
                while (!MemoryFileWindows.Exists(memoryName))
                {
                    Console.WriteLine("Waiting for Thunderscope bridge writer...");
                    Thread.Sleep(1000);
                }
            }
            else
            {
                while (!MemoryFileUnix.Exists(memoryName, Path.GetTempPath()))
                {
                    Console.WriteLine("Waiting for Thunderscope bridge writer...");
                    Thread.Sleep(1000);
                }
            }

            ulong dataCapacityBytes = 0;
            using (var headerReader = new ThunderscopeBridgeHeaderReader(memoryName))
            {
                dataCapacityBytes = headerReader.GetDataCapacityBytes();
            }

            // Now open the full bridge connection
            ulong bridgeCapacityBytes = (ulong)sizeof(ThunderscopeBridgeHeader) + dataCapacityBytes;
            file = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new MemoryFileWindows(memoryName, bridgeCapacityBytes)
                : new MemoryFileUnix(memoryName, bridgeCapacityBytes);

            try
            {
                view = file.MappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);

                try
                {
                    basePointer = GetPointer();
                    dataPointer = basePointer + sizeof(ThunderscopeBridgeHeader);

                    while (!IsHeaderSet)
                    {
                        Console.WriteLine("Waiting for Thunderscope bridge...");
                        Thread.Sleep(1000);
                    }
                    GetHeader();
                    dataRequestSemaphore = InterprocessSemaphore.CreateReleaser(memoryName + ".DataRequest");
                    dataResponseSemaphore = InterprocessSemaphore.CreateWaiter(memoryName + ".DataResponse");
                }
                catch
                {
                    view.Dispose();
                    throw;
                }
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            view.SafeMemoryMappedViewHandle.ReleasePointer();
            view.Flush();
            view.Dispose();
            file.Dispose();
        }

        public ThunderscopeConfiguration Configuration
        {
            get
            {
                GetHeader();
                return header.Configuration;
            }
        }

        public ThunderscopeProcessing Processing
        {
            get
            {
                GetHeader();
                return header.Processing;
            }
        }

        public ThunderscopeMonitoring Monitoring
        {
            get
            {
                GetHeader();
                return header.Monitoring;
            }
        }

        public bool RequestAndWaitForData(int millisecondsTimeout)
        {
            if (!hasSignaledRequest)
            {
                // Only signal request once, or we will run up semaphore counter
                dataRequestSemaphore.Release();
                hasSignaledRequest = true;
            }

            bool responded = dataResponseSemaphore.Wait(millisecondsTimeout);

            if (responded)
            {
                // Now that the bridge has tick-tocked, the next request will be 'real'
                // TODO: Should this be a separate method, or part of GetPointer() ?
                hasSignaledRequest = false;
            }

            return responded;
        }

        private void GetHeader()
        {
            unsafe { Unsafe.Copy(ref header, basePointer); }
        }

        //private void SetHeader()
        //{
        //    unsafe { Unsafe.Copy(basePointer, ref header); }
        //}

        private unsafe byte* GetPointer()
        {
            byte* ptr = null;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            if (ptr == null)
                throw new InvalidOperationException("Failed to acquire a pointer to the memory mapped file view.");

            return ptr;
        }

        private unsafe ReadOnlySpan<sbyte> GetAcquiredRegionI8()
        {
            int regionLength = (int)(header.Processing.CurrentChannelCount * header.Processing.CurrentChannelDataLength * header.Processing.CurrentChannelDataByteCount);
            return header.AcquiringRegion switch
            {
                ThunderscopeMemoryAcquiringRegion.RegionA => new ReadOnlySpan<sbyte>(dataPointer + regionLength, regionLength),        // If acquiring region is Region A, return Region B
                ThunderscopeMemoryAcquiringRegion.RegionB => new ReadOnlySpan<sbyte>(dataPointer, regionLength),                       // If acquiring region is Region B, return Region A
                _ => throw new InvalidDataException("Enum value not handled, add enum value to switch")
            };
        }

        private unsafe ReadOnlySpan<byte> GetAcquiredRegionU8()
        {
            int regionLength = (int)(header.Processing.CurrentChannelCount * header.Processing.CurrentChannelDataLength * header.Processing.CurrentChannelDataByteCount);
            return header.AcquiringRegion switch
            {
                ThunderscopeMemoryAcquiringRegion.RegionA => new ReadOnlySpan<byte>(dataPointer + regionLength, regionLength),        // If acquiring region is Region A, return Region B
                ThunderscopeMemoryAcquiringRegion.RegionB => new ReadOnlySpan<byte>(dataPointer, regionLength),                       // If acquiring region is Region B, return Region A
                _ => throw new InvalidDataException("Enum value not handled, add enum value to switch")
            };
        }

        // For use by TS.NET.Native.BridgeReader only
        public unsafe byte* GetAcquiredRegionPointer()
        {
            int regionLength = (int)(header.Processing.CurrentChannelCount * header.Processing.CurrentChannelDataLength * header.Processing.CurrentChannelDataByteCount);
            return header.AcquiringRegion switch
            {
                ThunderscopeMemoryAcquiringRegion.RegionA => dataPointer + regionLength,        // If acquiring region is Region A, return Region B
                ThunderscopeMemoryAcquiringRegion.RegionB => dataPointer,                       // If acquiring region is Region B, return Region A
                _ => throw new InvalidDataException("Enum value not handled, add enum value to switch")
            };
        }
    }

    public class ThunderscopeBridgeHeaderReader : IDisposable
    {
        private readonly IMemoryFile file;
        private readonly MemoryMappedViewAccessor view;
        private readonly ulong dataCapacityBytes;

        public unsafe ThunderscopeBridgeHeaderReader(string memoryName)
        {
            file = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new MemoryFileWindows(memoryName, (ulong)sizeof(ThunderscopeBridgeHeader))
                : new MemoryFileUnix(memoryName, (ulong)sizeof(ThunderscopeBridgeHeader));
            try
            {
                view = file.MappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);
                try
                {
                    using (var handle = view.SafeMemoryMappedViewHandle)
                    {
                        while (handle.Read<ThunderscopeBridgeHeader>(0).Version == 0)
                        {
                            Console.WriteLine("Waiting for Thunderscope bridge writer...");
                            Thread.Sleep(1000);
                        }
                        dataCapacityBytes = view.SafeMemoryMappedViewHandle.Read<ThunderscopeBridgeHeader>(0).DataCapacityBytes;
                    }
                }
                catch
                {
                    view.Dispose();
                    throw;
                }
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }

        public ulong GetDataCapacityBytes()
        {
            return dataCapacityBytes;
        }

        public void Dispose()
        {
            view.Dispose();
            file.Dispose();
        }
    }
}
