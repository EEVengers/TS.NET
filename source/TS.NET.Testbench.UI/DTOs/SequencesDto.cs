using TS.NET.Sequences;

namespace TS.NET.Testbench.UI;

public class SequencesDto : MessageDto
{
    public List<SequenceInfo> Sequences { get; init; } = [];

    public static SequencesDto CreateDefault() => new()
    {
        Type = "sequences",
        Sequences =
        [
            new SequenceInfo
            {
                Id = "self-calibration",
                Name = "Self calibration",
                Description = "No additional instruments required. Partial calibration."
            },
            new SequenceInfo
            {
                Id = "bench-calibration",
                Name = "Bench calibration",
                Description = "Requires 2x SDG2xxx signal generator. Full calibration."
            },
            new SequenceInfo
            {
                Id = "noise-verification",
                Name = "Noise verification",
                Description = "No additional instruments required. Verifies frontend noise & creates noise spectral density plots."
            },
            new SequenceInfo
            {
                Id = "bench-verification",
                Name = "Bench verification",
                Description = "Requires 2x SDG2xxx signal generator. Verifies frontend frequency response & creates gain plots."
            },
        ]
    };

    public static SequencesDto CreateFactoryDefault()
    {
        var dto = CreateDefault();
        dto.Sequences.AddRange(new SequenceInfo
        {
            Id = "factory-trim",
            Name = "Factory trim",
            Description = "Factory sequence. Adjust manual trims on the PCB."
        },
        new SequenceInfo
        {
            Id = "beta-tester-hwid",
            Name = "Beta tester HWID input",
            Description = "Factory sequence. For HWID input by beta testers."
        });
        return dto;
    }
}


