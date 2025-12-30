namespace TS.NET.Testbench.UI;

public class SequencesDto : MessageDto
{
    public List<SequenceInfoDto> Sequences { get; init; } = new();

    public static SequencesDto CreateDefault() => new SequencesDto
    {
        Type = "sequences",
        Sequences =
        [
            new SequenceInfoDto
            {
                Id = "self-calibration",
                Name = "Self calibration",
                Description = "No additional instruments required. Partial calibration."
            },
            new SequenceInfoDto
            {
                Id = "bench-calibration",
                Name = "Bench calibration",
                Description = "Requires 2x SDG2xxx signal generator. Full calibration."
            },
            new SequenceInfoDto
            {
                Id = "noise-verification",
                Name = "Noise verification",
                Description = "No additional instruments required. Verifies frontend noise & creates noise spectral density plots."
            },
            new SequenceInfoDto
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
        dto.Sequences.AddRange(new SequenceInfoDto
        {
            Id = "factory-trim",
            Name = "Factory trim",
            Description = "Factory sequence. Adjust manual trims on the PCB."
        },
        new SequenceInfoDto
        {
            Id = "beta-tester-hwid",
            Name = "Beta tester HWID input",
            Description = "Factory sequence. For HWID input by beta testers."
        });
        return dto;
    }
}

public class SequenceInfoDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
