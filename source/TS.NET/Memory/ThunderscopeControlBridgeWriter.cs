using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using TS.NET.Memory.Unix;
using TS.NET.Memory.Windows;

namespace TS.NET
{
    // This is a shared memory-mapped file between processes, with only a single writer and a single reader
    // Not thread safe
    public class ThunderscopeControlBridgeWriter : IDisposable
    {
        private readonly IMemoryFile file;
        private readonly MemoryMappedViewAccessor view;
        private unsafe byte* basePointer;
        private ThunderscopeControlBridgeContent content;
        private bool IsContentSet { get { GetContent(); return content.Version != 0; } }
        private readonly IInterprocessSemaphoreReleaser controlUpdateSemaphore;

        public unsafe ThunderscopeControlBridgeWriter(string memoryName)
        {
            memoryName += ".Control";
            if (OperatingSystem.IsWindows())
            {
                while (!MemoryFileWindows.Exists(memoryName))
                {
                    Console.WriteLine("Waiting for Thunderscope control bridge reader to create MMF...");
                    Thread.Sleep(1000);
                }

                file = new MemoryFileWindows(memoryName);
            }
            else
            {
                while (!MemoryFileUnix.Exists(memoryName))
                {
                    Console.WriteLine("Waiting for Thunderscope control bridge reader to create MMF...");
                    Thread.Sleep(1000);
                }

                file = new MemoryFileUnix(memoryName, (ulong)sizeof(ThunderscopeControlBridgeContent));
            }

            try
            {
                view = file.MappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);

                try
                {
                    basePointer = GetPointer();
                    while (!IsContentSet)
                    {
                        Console.WriteLine("Waiting for Thunderscope control bridge reader to set content...");
                        Thread.Sleep(1000);
                    }
                    GetContent();
                    controlUpdateSemaphore = InterprocessSemaphore.CreateReleaser(memoryName + ".ControlUpdate", 0);
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
            set
            {
                // This is a shallow copy, but considering the struct should be 100% blitable (i.e. no reference types), this is effectively a full copy
                content.Hardware = value;
                SetContent();
            }
        }

        public ThunderscopeProcessingConfig Processing
        {
            set
            {
                // This is a shallow copy, but considering the struct should be 100% blitable (i.e. no reference types), this is effectively a full copy
                content.Processing = value;
                SetContent();
            }
        }

        private void GetContent()
        {
            unsafe { Unsafe.Copy(ref content, basePointer); }
        }

        private void SetContent()
        {
            unsafe { Unsafe.Copy(basePointer, ref content); }
            controlUpdateSemaphore.Release();
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
