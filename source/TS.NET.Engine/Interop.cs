using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TS.NET.Engine
{
    internal static partial class Interop
    {
        [LibraryImport("kernel32.dll")]
        public static partial int GetCurrentThreadId();

        [LibraryImport("kernel32.dll")]
        public static partial int GetCurrentProcessorNumber();

        internal static ProcessThread CurrentThread
        {
            get
            {
                int id = GetCurrentThreadId();
                return Process.GetCurrentProcess().Threads.Cast<ProcessThread>().Single(x => x.Id == id);
            }
        }
    }
}
