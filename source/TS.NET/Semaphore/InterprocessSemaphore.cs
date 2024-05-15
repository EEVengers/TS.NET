using System.Runtime.InteropServices;
using TS.NET.Semaphore.Linux;
using TS.NET.Semaphore.MacOS;
using TS.NET.Semaphore.Windows;

namespace TS.NET
{
    /// <summary>
    /// This class opens or creates platform agnostic named semaphore. Named
    /// semaphores are synchronization constructs accessible across processes.
    /// </summary>
    public static class InterprocessSemaphore
    {
        public static IInterprocessSemaphoreWaiter CreateWaiter(string name, int initialCount)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return new SemaphoreWindows(name, initialCount);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return new SemaphoreMacOS(name);

            return new SemaphoreLinux(name, (uint)initialCount);
        }

        public static IInterprocessSemaphoreReleaser CreateReleaser(string name, int initialCount)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return new SemaphoreWindows(name, initialCount);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return new SemaphoreMacOS(name);

            return new SemaphoreLinux(name, (uint)initialCount);
        }
    }
}
