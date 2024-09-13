using System;

namespace TS.NET
{
    public interface IInterprocessSemaphoreWaiter : IDisposable
    {
        /// <summary>
        /// Decrement the semaphore, waiting if value is zero.
        /// </summary>
        bool Wait(int millisecondsTimeout);
    }
}
