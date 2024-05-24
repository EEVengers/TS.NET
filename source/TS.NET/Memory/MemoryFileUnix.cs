﻿// https://github.com/cloudtoid/interprocess
using System.IO.MemoryMappedFiles;

namespace TS.NET.Memory.Unix
{
    internal sealed class MemoryFileUnix : IMemoryFile
    {
        private const FileAccess FileAccessOption = FileAccess.ReadWrite;
        private const FileShare FileShareOption = FileShare.ReadWrite | FileShare.Delete;
        private const string MapNamePrefix = "TS.NET.";
        private const int BufferSize = 0x1000;
        private readonly string file;

        internal MemoryFileUnix(string memoryName, ulong bridgeCapacityBytes)
        {
            // https://github.com/dotnet/runtime/blob/main/src/libraries/System.IO.MemoryMappedFiles/src/System/IO/MemoryMappedFiles/MemoryMappedFile.Unix.cs
            file = Path.Combine("/dev/shm", MapNamePrefix + memoryName);
            FileStream stream;
            if (IsFileInUse(file))
            {
                // just open the file
                stream = new FileStream(
                    file,
                    FileMode.Open, // just open it
                    FileAccessOption,
                    FileShareOption,
                    BufferSize);
            }
            else
            {
                // override (or create if no longer exist) as it is not being used
                stream = new FileStream(
                    file,
                    FileMode.Create,
                    FileAccessOption,
                    FileShareOption,
                    BufferSize);
                //File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }

            try
            {
                MappedFile = MemoryMappedFile.CreateFromFile(
                    stream,
                    mapName: null, // do not set this or it will not work on Linux/Unix/MacOS
                    (long)bridgeCapacityBytes,
                    MemoryMappedFileAccess.ReadWrite,
                    HandleInheritability.None,
                    false);
            }
            catch
            {
                // do not leave any resources hanging
                try
                {
                    stream.Dispose();
                }
                catch
                {
                    ResetBackingFile();
                }

                throw;
            }
        }

        ~MemoryFileUnix()
           => Dispose(false);

        public MemoryMappedFile MappedFile { get; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                    MappedFile.Dispose();
            }
            finally
            {
                ResetBackingFile();
            }
        }

        private void ResetBackingFile()
        {
            // Deletes the backing file if it is not used by any other process

            if (IsFileInUse(file))
                return;

            //if (!PathUtil.TryDeleteFile(file))
            //    logger.LogError("Failed to delete queue's shared memory backing file even though it is not in use by any process.");
        }

        private static bool IsFileInUse(string file)
        {
            try
            {
                using (new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None)) { }
                return false;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (IOException)
            {
                return true;
            }
        }

        public static bool Exists(string memoryName)
        {
            var file = Path.Combine("/dev/shm", MapNamePrefix + memoryName);
            return IsFileInUse(file);
        }

        public static ulong Size(string memoryName)
        {
            var file = Path.Combine("/dev/shm", MapNamePrefix + memoryName);
            using var stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete, 0x1000);
            return (ulong)stream.Length;
        }
    }
}
