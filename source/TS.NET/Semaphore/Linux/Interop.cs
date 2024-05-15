﻿using System.Runtime.InteropServices;
using TS.NET.Semaphore.Posix;

namespace TS.NET.Semaphore.Linux
{
    internal static partial class Interop
    {
        private const string Lib = "librt.so.1";
        private const uint SEMVALUEMAX = 32767;
        private const int OCREAT = 0x040;    // Create the semaphore if it does not exist

        private const int ENOENT = 2;        // The named semaphore does not exist.
        private const int EINTR = 4;         // Semaphore operation was interrupted by a signal.
        private const int EAGAIN = 11;       // Couldn't be acquired (sem_trywait)
        private const int ENOMEM = 12;       // Out of memory
        private const int EACCES = 13;       // Semaphore exists, but the caller does not have permission to open it.
        private const int EEXIST = 17;       // O_CREAT and O_EXCL were specified and the semaphore exists.
        private const int EINVAL = 22;       // Invalid semaphore or operation on a semaphore
        private const int ENFILE = 23;       // Too many semaphores or file descriptors are open on the system.
        private const int EMFILE = 24;       // The process has already reached its limit for semaphores or file descriptors in use.
        private const int ENAMETOOLONG = 36; // The specified semaphore name is too long
        private const int EOVERFLOW = 75;    // The maximum allowable value for a semaphore would be exceeded.
        private const int ETIMEDOUT = 110;   // The call timed out before the semaphore could be locked.

        private static unsafe int Error => Marshal.GetLastWin32Error();

        [LibraryImport(Lib, EntryPoint = "sem_open", StringMarshalling = StringMarshalling.Utf8)]
        private static partial IntPtr SemaphoreOpen(string name, int oflag, uint mode, uint value);

        [LibraryImport(Lib, EntryPoint = "sem_post")]
        private static partial int SemaphorePost(IntPtr handle);

        [LibraryImport(Lib, EntryPoint = "sem_wait")]
        private static partial int SemaphoreWait(IntPtr handle);

        [LibraryImport(Lib, EntryPoint = "sem_trywait")]
        private static partial int SemaphoreTryWait(IntPtr handle);

        [LibraryImport(Lib, EntryPoint = "sem_timedwait")]
        private static partial int SemaphoreTimedWait(IntPtr handle, ref PosixTimespec abs_timeout);

        [LibraryImport(Lib, EntryPoint = "sem_unlink", StringMarshalling = StringMarshalling.Utf8)]
        private static partial int SemaphoreUnlink(string name);

        [LibraryImport(Lib, EntryPoint = "sem_close")]
        private static partial int SemaphoreClose(IntPtr handle);

        internal static IntPtr CreateOrOpenSemaphore(string name, uint initialCount)
        {
            var handle = SemaphoreOpen(name, OCREAT, (uint)PosixFilePermissions.ACCESSPERMS, initialCount);
            if (handle != IntPtr.Zero)
                return handle;

            throw Error switch
            {
                EINVAL => new ArgumentException($"The initial count cannot be greater than {SEMVALUEMAX}.", nameof(initialCount)),
                ENAMETOOLONG => new ArgumentException($"The specified semaphore name is too long.", nameof(name)),
                EACCES => new PosixSempahoreUnauthorizedAccessException(),
                EEXIST => new PosixSempahoreExistsException(),
                EINTR => new OperationCanceledException(),
                ENFILE => new PosixSempahoreException("Too many semaphores or file descriptors are open on the system."),
                EMFILE => new PosixSempahoreException("Too many semaphores or file descriptors are open by this process."),
                ENOMEM => new InsufficientMemoryException(),
                _ => new PosixSempahoreException(Error),
            };
        }

        internal static void Release(IntPtr handle)
        {
            if (SemaphorePost(handle) == 0)
                return;

            throw Error switch
            {
                EINVAL => new InvalidPosixSempahoreException(),
                EOVERFLOW => new SemaphoreFullException(),
                _ => new PosixSempahoreException(Error),
            };
        }

        internal static bool Wait(IntPtr handle, int millisecondsTimeout)
        {
            if (millisecondsTimeout == Timeout.Infinite)
            {
                Wait(handle);
                return true;
            }
            else if (millisecondsTimeout == 0)
            {
                if (SemaphoreTryWait(handle) == 0)
                    return true;

                return Error switch
                {
                    EAGAIN => false,
                    EINVAL => throw new InvalidPosixSempahoreException(),
                    EINTR => throw new OperationCanceledException(),
                    _ => throw new PosixSempahoreException(Error),
                };
            }

            var timeout = DateTimeOffset.UtcNow.AddMilliseconds(millisecondsTimeout);
            return Wait(handle, timeout);
        }

        private static void Wait(IntPtr handle)
        {
            if (SemaphoreWait(handle) == 0)
                return;

            throw Error switch
            {
                EINVAL => new InvalidPosixSempahoreException(),
                EINTR => new OperationCanceledException(),
                _ => new PosixSempahoreException(Error),
            };
        }

        private static bool Wait(IntPtr handle, PosixTimespec timeout)
        {
            if (SemaphoreTimedWait(handle, ref timeout) == 0)
                return true;

            return Error switch
            {
                ETIMEDOUT => false,
                EINVAL => throw new InvalidPosixSempahoreException(),
                EINTR => throw new OperationCanceledException(),
                _ => throw new PosixSempahoreException(Error),
            };
        }

        internal static void Close(IntPtr handle)
        {
            if (SemaphoreClose(handle) == 0)
                return;

            throw Error switch
            {
                EINVAL => new InvalidPosixSempahoreException(),
                _ => new PosixSempahoreException(Error),
            };
        }

        internal static void Unlink(string name)
        {
            if (SemaphoreUnlink(name) == 0)
                return;

            throw Error switch
            {
                ENAMETOOLONG => new ArgumentException($"The specified semaphore name is too long.", nameof(name)),
                EACCES => new PosixSempahoreUnauthorizedAccessException(),
                ENOENT => new PosixSempahoreNotExistsException(),
                _ => new PosixSempahoreException(Error),
            };
        }
    }
}