namespace TS.NET.Engine
{
    internal class EmptyWaveformBufferReader : IEngineTask
    {
        public void Start(SemaphoreSlim startSemaphore)
        {
            startSemaphore.Release();
        }

        public void Stop() { }
    }
}
