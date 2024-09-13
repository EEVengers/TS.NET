using System;

namespace TS.NET
{
    public interface IInterprocessSemaphoreReleaser : IDisposable
    {
        /// <summary>
        /// Increment the semaphore.
        /// </summary>
        void Release();
    }
}
