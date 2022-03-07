using System;

namespace TS.NET
{
    internal interface IInterprocessSemaphoreReleaser : IDisposable
    {
        void Release();
    }
}
