namespace TS.NET.Engine
{
    internal interface IEngineTask
    {
        void Start(SemaphoreSlim startSemaphore);
        void Stop();
    }
}
