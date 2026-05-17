using TS.NET.Sequences;

namespace TS.NET.Testbench.UI;

public class SequencesDto : MessageDto
{
    private static readonly SequenceInfoDto[] AllSequences =
    [
        new()
        {
            Id = "self-calibration",
            Name = "Self calibration",
            Description = "No additional instruments required. Partial calibration.",
            Type = "user"
        },
        new()
        {
            Id = "noise-verification",
            Name = "Noise verification",
            Description = "No additional instruments required. Verifies frontend noise.",
            Type = "user"
        },
        new()
        {
            Id = "developer-calibration",
            Name = "Developer calibration",
            Description = "Developer sequence. Full calibration. Requires 2x SDG2xxx.",
            Type = "developer"
        },
        new()
        {
            Id = "developer-verification",
            Name = "Developer verification",
            Description = "Developer sequence. Verifies performance. Requires 2x SDG2xxx.",
            Type = "developer"
        },
        new()
        {
            Id = "developer-hwid",
            Name = "Developer HWID",
            Description = "Developer sequence. HWID input.",
            Type = "developer"
        },
        new()
        {
            Id = "factory-bring-up",
            Name = "Factory bring-up",
            Description = "Factory sequence. JTAG programming & HWID. Requires JTAG-HS2.",
            Type = "factory"
        },
        new()
        {
            Id = "factory-trim",
            Name = "Factory trim",
            Description = "Factory sequence. Adjust manual trims on the PCB. Requires factory setup.",
            Type = "factory"
        },
        new()
        {
            Id = "factory-calibration",
            Name = "Factory calibration",
            Description = "Factory sequence. Full calibration. Requires factory setup.",
            Type = "factory"
        },
        new()
        {
            Id = "factory-verification",
            Name = "Factory verification",
            Description = "Factory sequence. Verifies performance. Requires factory setup.",
            Type = "factory"
        }
    ];

    public List<SequenceInfoDto> Sequences { get; init; } = [];

    public static SequencesDto CreateForTypes(IEnumerable<string>? sequenceTypes)
    {
        var enabledTypes = (sequenceTypes ?? [])
            .Where(static sequenceType => !string.IsNullOrWhiteSpace(sequenceType))
            .Select(static sequenceType => sequenceType.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new SequencesDto
        {
            Type = "sequences",
            Sequences = AllSequences
                .Where(sequence => enabledTypes.Contains(sequence.Type))
                .ToList()
        };
    }
}

public class SequenceInfoDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Type { get; init; }
}


