// https://github.com/cloudtoid/interprocess
using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using TS.NET.Memory.Unix;
using TS.NET.Memory.Windows;
using Microsoft.Extensions.Logging;
using TS.NET.Memory;

namespace TS.NET
{
    // This is a shared memory-mapped file between processes, with only a single writer and a single reader using a primitive syncronisation header
    public class Postbox : IDisposable
    {
        private readonly IMemoryFile file;
        private readonly MemoryMappedViewAccessor view;

        public unsafe Postbox(PostboxOptions options, ILoggerFactory loggerFactory)
        {
            file = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? new MemoryFileWindows(options)
                : new MemoryFileUnix(options, loggerFactory);

            try
            {
                view = file.MappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.ReadWrite);

                try
                {
                    BasePointer = AcquirePointer();
                    DataPointer = BasePointer + sizeof(MemoryHeader);
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

        public unsafe byte* BasePointer { get; }
        public unsafe byte* DataPointer { get; }

        public void Dispose()
        {
            view.SafeMemoryMappedViewHandle.ReleasePointer();
            view.Flush();
            view.Dispose();
            file.Dispose();
        }

        private unsafe byte* AcquirePointer()
        {
            byte* ptr = null;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
            if (ptr == null)
                throw new InvalidOperationException("Failed to acquire a pointer to the memory mapped file view.");

            return ptr;
        }

        public unsafe bool IsReadyToWrite()
        {
            return BasePointer[0] == (byte)PostboxState.Empty;
        }

        public unsafe bool IsReadyToRead()
        {
            return BasePointer[0] == (byte)PostboxState.Full;
        }

        public unsafe void DataIsWritten()
        {
            BasePointer[0] = (byte)PostboxState.Full;
        }

        public unsafe void DataIsRead()
        {
            BasePointer[0] = (byte)PostboxState.Empty;
        }
    }
}
