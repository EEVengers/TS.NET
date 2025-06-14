namespace TS.NET.Engine
{
    internal class MemoryMappedFile : IEngineTask
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
}
