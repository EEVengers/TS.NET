using TS.NET.Sequencer;

namespace TS.NET.Testbench.UI;

public class StepDto
{
    public int Index { get; set; }
    public string? Name { get; set; }
    public StepResultDto? Result { get; set; }
    public bool Skip { get; set; }
    public bool IgnoreError { get; set; }

    // UI
    public bool AllowSkip { get; set; }

    internal static StepDto FromStep(Step step)
    {
        StepResultDto? result = null;
        if (step.Result != null)
        {
            result = new StepResultDto()
            {
                Status = step.Result!.Status,
                Duration = TimeSpanUtility.HumanDuration(step.Result!.Duration)
            };
        }
        return new StepDto()
        {
            Index = step.Index,
            Name = step.Name,
            Result = result,
            Skip = step.Skip,
            IgnoreError = step.IgnoreError,
            AllowSkip = step.AllowSkip
        };
    }
}

public class StepResultDto
{
    public Status? Status { get; set; }
    public string? Duration { get; set; }
}