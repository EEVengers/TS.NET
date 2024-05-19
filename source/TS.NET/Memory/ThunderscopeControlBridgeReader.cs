using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TS.NET.Memory.Unix;
using TS.NET.Memory.Windows;

namespace TS.NET
{
    // This is a shared memory-mapped file between processes, with only a single writer and a single reader with a header struct
    public class ThunderscopeControlBridgeReader : IDisposable
    {
        private readonly IMemoryFile file;
        private readonly MemoryMappedViewAccessor view;
        private unsafe byte* basePointer;
        private ThunderscopeControlBridgeContent content;
        private readonly IInterprocessSemaphoreWaiter controlUpdateSemaphore;

        public unsafe ThunderscopeControlBridgeReader(string memoryName, ThunderscopeDataBridgeConfig dataBridgeConfig)
        {
            memoryName += ".Control";
            var bridgeCapacityBytes = (ulong)sizeof(ThunderscopeControlBridgeContent);
            file = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new MemoryFileWindows(memoryName, bridgeCapacityBytes)
                : new MemoryFileUnix(memoryName, bridgeCapacityBytes);

            try
            {
                view = file.MappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);

                try
                {
                    basePointer = GetPointer();
                    // Reader sets initial state of header
                    content.Version = 1;
                    content.DataBridge = dataBridgeConfig;
                    SetHeader();
                    controlUpdateSemaphore = InterprocessSemaphore.CreateWaiter(memoryName + ".ControlUpdate", 0);
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

        public ThunderscopeHardwareConfig Hardware
        {
            get
            {
                GetHeader();
                return content.Hardware;
            }
        }

        public ThunderscopeProcessingConfig Processing
        {
            get
            {
                GetHeader();
                return content.Processing;
            }
        }

        public bool WaitForUpdate(int millisecondsTimeout, out ThunderscopeHardwareConfig hardware, out ThunderscopeProcessingConfig processing)
        {
            var request = controlUpdateSemaphore.Wait(millisecondsTimeout);
            if (request) while (controlUpdateSemaphore.Wait(0)) { }  // Run down the semaphore in rare edge cases
            if (request)
            {
                GetHeader();
                hardware = content.Hardware;
                processing = content.Processing;
                return true;
            }
            hardware = default;
            processing = default;
            return false;
        }

        private void GetHeader()
        {
            unsafe { Unsafe.Copy(ref content, basePointer); }
        }

        private void SetHeader()
        {
            unsafe { Unsafe.Copy(basePointer, ref content); }
        }

        private unsafe byte* GetPointer()
        {
            byte* ptr = null;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            if (ptr == null)
                throw new InvalidOperationException("Failed to acquire a pointer to the memory mapped file view.");

            return ptr;
        }
    }
}
