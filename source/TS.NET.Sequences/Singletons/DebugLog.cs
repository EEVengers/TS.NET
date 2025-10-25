namespace TS.NET.Sequences;

public class DebugLog
{
    private static readonly Lazy<DebugLog> lazy = new(() => new DebugLog());
    public static DebugLog Instance { get { return lazy.Value; } }

    private static StreamWriter? streamWriter;
    private static readonly string logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DebugLog.txt");

    public void Log(string message)
    {
        try
        {
            streamWriter = new StreamWriter(logFilePath, append: true);
            streamWriter.WriteLine(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing to debug file: {ex.Message}");
        }
        finally
        {
            streamWriter?.Close();
        }
    }
}
