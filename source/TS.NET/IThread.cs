namespace TS.NET;

public interface IThread
{
    void Start(SemaphoreSlim startSemaphore);
    void Stop();
}
