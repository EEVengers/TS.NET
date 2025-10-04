namespace TS.NET.Engine;

internal interface IThread
{
    void Start(SemaphoreSlim startSemaphore);
    void Stop();
}
