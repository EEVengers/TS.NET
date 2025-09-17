using TS.NET.Sequencer;

namespace TS.NET.Calibration.UI;

public class SequenceDto : MessageDto
{
    public string? Name { get; set; }
    public StepDto[]? Steps { get; set; }
    public Status? Status { get; set; }
    public string? Duration { get; set; }

    internal static SequenceDto FromSequence(Sequence sequence)
    {
        StepDto[]? steps = null;
        if (sequence.Steps != null)
        {
            steps = new StepDto[sequence.Steps.Length];
            for (int i = 0; i < sequence.Steps.Length; i++)
            {
                steps[i] = StepDto.FromStep(sequence.Steps[i]);
            }
        }
        return new SequenceDto()
        {
            Type = "sequence",
            Name = sequence.Name,
            Steps = steps,
            Status = sequence.Status,
            Duration = FormatDuration(sequence.Duration)
        };
    }

    private static string? FormatDuration(TimeSpan? duration)
    {
        if (duration == null)
            return "-";
        string format;
        if (duration?.Hours > 0)
            format = @"h\h\ m\m\ s\s";
        else if (duration?.Minutes > 0)
            format = @"m\m\ s\s";
        else
            format = @"s\s";
        return ((TimeSpan)duration!).ToString(format);
    }
}