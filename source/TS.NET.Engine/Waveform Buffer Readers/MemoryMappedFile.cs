namespace TS.NET.Engine;

internal class MemoryMappedFile : IThread
{
    public void Start(SemaphoreSlim startSemaphore)
    {
        startSemaphore.Release();
    }

    public void Stop()
    {
        throw new NotImplementedException();
    }
}
