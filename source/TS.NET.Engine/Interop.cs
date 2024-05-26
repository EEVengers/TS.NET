using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TS.NET.Engine
{
    internal static partial class OsThread
    {      
        [LibraryImport("libc.so.6")]
        private static partial int gettid();

        //[LibraryImport("libc.so.6", EntryPoint = "pthread_setaffinity_np")]//, SetLastError=true)]
        //public static partial int ThreadSetAffinity(ulong thread, ulong cpusetsize, ulong cpuset);

        [LibraryImport("libc.so.6", EntryPoint = "sched_setaffinity", SetLastError=true)]
        public static partial int SchedulerSetAffinity(int thread, ulong cpusetsize, IntPtr cpuset);

        //[DllImport("libc.so.6")]
        //static extern int pthread_self();

        [LibraryImport("kernel32.dll")]
        public static partial int GetCurrentThreadId();

        [SupportedOSPlatform("Windows")]
        [SupportedOSPlatform("Linux")]
        [UnsupportedOSPlatform("OSX")]
        public static int GetCurrentOsThreadId()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return gettid();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return GetCurrentThreadId();
            }

            throw new NotSupportedException("Unable to get unmanaged thread id on this platform");
        }

        [SupportedOSPlatform("Windows")]
        [SupportedOSPlatform("Linux")]
        [UnsupportedOSPlatform("OSX")]
        public static void SetThreadAffinity(int cpu)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                int thread = GetCurrentOsThreadId();

                ulong cpuMask = 1UL << cpu;
                IntPtr cpuSet = Marshal.AllocHGlobal(Marshal.SizeOf(cpuMask));
                Marshal.StructureToPtr(cpuMask, cpuSet, false);

                int result = SchedulerSetAffinity(thread, (ulong)Marshal.SizeOf(cpuMask), cpuSet);
                if(result > 0)
                    throw new Exception("SchedulerSetAffinity not successful");
                return;
            }

            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                int id = GetCurrentOsThreadId();
                var thread = Process.GetCurrentProcess().Threads.Cast<ProcessThread>().Single(x => x.Id == id);
                thread.ProcessorAffinity = new IntPtr(1 << cpu);
                return;
            }

            throw new NotSupportedException("Unable to set thread affinity on this platform");
        }
    }
}
