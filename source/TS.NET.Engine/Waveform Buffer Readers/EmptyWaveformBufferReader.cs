namespace TS.NET.Engine
{
    internal class EmptyWaveformBufferReader : IThread
    {
        public void Start(SemaphoreSlim startSemaphore)
        {
            startSemaphore.Release();
        }

        public void Stop() { }
    }
}
