namespace TS.NET.Sequencer;

public class StepResult
{
    public Status? Status { get; set; }
    public TimeSpan? Duration { get; set; }
    public Exception? Exception { get; set; }
    public string? Summary { get; set; }
    public List<IStepResultMetadata>? Metadata { get; set; }
}
