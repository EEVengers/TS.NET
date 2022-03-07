using System;

namespace TS.NET
{
    internal interface IInterprocessSemaphoreWaiter : IDisposable
    {
        bool Wait(int millisecondsTimeout);
    }
}
