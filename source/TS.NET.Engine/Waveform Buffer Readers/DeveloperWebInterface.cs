namespace TS.NET.Engine
{
    internal class DeveloperWebInterface : IEngineTask
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
