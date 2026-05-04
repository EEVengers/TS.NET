namespace TS.NET.Sequences;

public class CalibrationVariables : CommonVariables
{
    public string? CalibrationFileName { get; set; } = "thunderscope-calibration.json";
    public int ParametersSet { get; set; }
    public DateTimeOffset CalibrationTimestamp { get; set; }

    // Lowest dpot value is 4, which gives around 1.9V to 3.1V at the PGA negative input - the allowable CM range.
    // The ramp up of targetDPotRes on the highest gain configs is to give good range with the attenuator on.
    // The targetDPotRes still tends to be above noise floor of the frontend so it balances out well.
    // targetDPotRes of 3 is about vertical 80 divisions (10 minor divisions within 8 major divisions)

    public ChannelPathConfig[] Channel1PathConfigs { get; set; } = [
        new ChannelPathConfig(PgaPreampGain.High, 0, 12, targetDPotRes: 3, sgAmpStep: 0.0002, sgAmpStart: 15 * 0.0002),    // 30dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 1, 12, targetDPotRes: 2.38, sgAmpStep: 0.0003, sgAmpStart: 15 * 0.0003), // 28dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 2, 12, targetDPotRes: 1.9, sgAmpStep: 0.0004, sgAmpStart: 15 * 0.0004),  // 26dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 3, 12, targetDPotRes: 1.5, sgAmpStep: 0.0005, sgAmpStart: 15 * 0.0005),  // 24dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 4, 12, targetDPotRes: 1.22, sgAmpStep: 0.0006, sgAmpStart: 15 * 0.0006), // 22dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 5, 12, targetDPotRes: 0.98, sgAmpStep: 0.0008, sgAmpStart: 15 * 0.0008), // 20dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 6, 12, targetDPotRes: 0.78, sgAmpStep: 0.0010, sgAmpStart: 15 * 0.0010), // 18dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 7, 12, targetDPotRes: 0.625, sgAmpStep: 0.0012, sgAmpStart: 15 * 0.0012),// 16dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 8, 12, targetDPotRes: 0.5, sgAmpStep: 0.0016, sgAmpStart: 15 * 0.0016),  // 14dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 9,  9, targetDPotRes: 0.5, sgAmpStep: 0.0020, sgAmpStart: 15 * 0.0020),  // 12dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 10, 7, targetDPotRes: 0.5, sgAmpStep: 0.0025, sgAmpStart: 15 * 0.0025),  // 10dB + 8.86dB

        new ChannelPathConfig(PgaPreampGain.Low, 0,  7, targetDPotRes: 0.5, sgAmpStep: 0.0025, sgAmpStart: 15 * 0.0025),   // 10dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 1,  5, targetDPotRes: 0.5, sgAmpStep: 0.0031, sgAmpStart: 15 * 0.0031),   // 8dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 2,  4, targetDPotRes: 0.5, sgAmpStep: 0.0039, sgAmpStart: 15 * 0.0039),   // 6dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 3,  4, targetDPotRes: 0.5, sgAmpStep: 0.0049, sgAmpStart: 15 * 0.0049),   // 4dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 4,  4, targetDPotRes: 0.5, sgAmpStep: 0.0062, sgAmpStart: 15 * 0.0062),   // 2dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 5,  4, targetDPotRes: 0.5, sgAmpStep: 0.0078, sgAmpStart: 15 * 0.0078),   // 0dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 6,  4, targetDPotRes: 0.5, sgAmpStep: 0.0098, sgAmpStart: 15 * 0.0098),   // -2dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 7,  4, targetDPotRes: 0.5, sgAmpStep: 0.0123, sgAmpStart: 15 * 0.0123),   // -4dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 8,  4, targetDPotRes: 0.5, sgAmpStep: 0.0155, sgAmpStart: 15 * 0.0155),   // -6dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 9,  4, targetDPotRes: 0.5, sgAmpStep: 0.0194, sgAmpStart: 15 * 0.0194),   // -8dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 10, 4, targetDPotRes: 0.5, sgAmpStep: 0.0244, sgAmpStart: 15 * 0.0244)    // -10dB + 8.86dB
];

    public ChannelPathConfig[] Channel2PathConfigs { get; set; } = [
        new ChannelPathConfig(PgaPreampGain.High, 0, 12, targetDPotRes: 3, sgAmpStep: 0.0002, sgAmpStart: 15 * 0.0002),    // 30dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 1, 12, targetDPotRes: 2.38, sgAmpStep: 0.0003, sgAmpStart: 15 * 0.0003), // 28dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 2, 12, targetDPotRes: 1.9, sgAmpStep: 0.0004, sgAmpStart: 15 * 0.0004),  // 26dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 3, 12, targetDPotRes: 1.5, sgAmpStep: 0.0005, sgAmpStart: 15 * 0.0005),  // 24dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 4, 12, targetDPotRes: 1.22, sgAmpStep: 0.0006, sgAmpStart: 15 * 0.0006), // 22dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 5, 12, targetDPotRes: 0.98, sgAmpStep: 0.0008, sgAmpStart: 15 * 0.0008), // 20dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 6, 12, targetDPotRes: 0.78, sgAmpStep: 0.0010, sgAmpStart: 15 * 0.0010), // 18dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 7, 12, targetDPotRes: 0.625, sgAmpStep: 0.0012, sgAmpStart: 15 * 0.0012),// 16dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 8, 12, targetDPotRes: 0.5, sgAmpStep: 0.0016, sgAmpStart: 15 * 0.0016),  // 14dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 9,  9, targetDPotRes: 0.5, sgAmpStep: 0.0020, sgAmpStart: 15 * 0.0020),  // 12dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 10, 7, targetDPotRes: 0.5, sgAmpStep: 0.0025, sgAmpStart: 15 * 0.0025),  // 10dB + 8.86dB

        new ChannelPathConfig(PgaPreampGain.Low, 0,  7, targetDPotRes: 0.5, sgAmpStep: 0.0025, sgAmpStart: 15 * 0.0025),   // 10dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 1,  5, targetDPotRes: 0.5, sgAmpStep: 0.0031, sgAmpStart: 15 * 0.0031),   // 8dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 2,  4, targetDPotRes: 0.5, sgAmpStep: 0.0039, sgAmpStart: 15 * 0.0039),   // 6dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 3,  4, targetDPotRes: 0.5, sgAmpStep: 0.0049, sgAmpStart: 15 * 0.0049),   // 4dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 4,  4, targetDPotRes: 0.5, sgAmpStep: 0.0062, sgAmpStart: 15 * 0.0062),   // 2dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 5,  4, targetDPotRes: 0.5, sgAmpStep: 0.0078, sgAmpStart: 15 * 0.0078),   // 0dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 6,  4, targetDPotRes: 0.5, sgAmpStep: 0.0098, sgAmpStart: 15 * 0.0098),   // -2dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 7,  4, targetDPotRes: 0.5, sgAmpStep: 0.0123, sgAmpStart: 15 * 0.0123),   // -4dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 8,  4, targetDPotRes: 0.5, sgAmpStep: 0.0155, sgAmpStart: 15 * 0.0155),   // -6dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 9,  4, targetDPotRes: 0.5, sgAmpStep: 0.0194, sgAmpStart: 15 * 0.0194),   // -8dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 10, 4, targetDPotRes: 0.5, sgAmpStep: 0.0244, sgAmpStart: 15 * 0.0244)    // -10dB + 8.86dB
    ];

    public ChannelPathConfig[] Channel3PathConfigs { get; set; } = [
        new ChannelPathConfig(PgaPreampGain.High, 0, 12, targetDPotRes: 3, sgAmpStep: 0.0002, sgAmpStart: 15 * 0.0002),    // 30dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 1, 12, targetDPotRes: 2.38, sgAmpStep: 0.0003, sgAmpStart: 15 * 0.0003), // 28dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 2, 12, targetDPotRes: 1.9, sgAmpStep: 0.0004, sgAmpStart: 15 * 0.0004),  // 26dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 3, 12, targetDPotRes: 1.5, sgAmpStep: 0.0005, sgAmpStart: 15 * 0.0005),  // 24dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 4, 12, targetDPotRes: 1.22, sgAmpStep: 0.0006, sgAmpStart: 15 * 0.0006), // 22dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 5, 12, targetDPotRes: 0.98, sgAmpStep: 0.0008, sgAmpStart: 15 * 0.0008), // 20dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 6, 12, targetDPotRes: 0.78, sgAmpStep: 0.0010, sgAmpStart: 15 * 0.0010), // 18dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 7, 12, targetDPotRes: 0.625, sgAmpStep: 0.0012, sgAmpStart: 15 * 0.0012),// 16dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 8, 12, targetDPotRes: 0.5, sgAmpStep: 0.0016, sgAmpStart: 15 * 0.0016),  // 14dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 9,  9, targetDPotRes: 0.5, sgAmpStep: 0.0020, sgAmpStart: 15 * 0.0020),  // 12dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 10, 7, targetDPotRes: 0.5, sgAmpStep: 0.0025, sgAmpStart: 15 * 0.0025),  // 10dB + 8.86dB

        new ChannelPathConfig(PgaPreampGain.Low, 0,  7, targetDPotRes: 0.5, sgAmpStep: 0.0025, sgAmpStart: 15 * 0.0025),   // 10dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 1,  5, targetDPotRes: 0.5, sgAmpStep: 0.0031, sgAmpStart: 15 * 0.0031),   // 8dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 2,  4, targetDPotRes: 0.5, sgAmpStep: 0.0039, sgAmpStart: 15 * 0.0039),   // 6dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 3,  4, targetDPotRes: 0.5, sgAmpStep: 0.0049, sgAmpStart: 15 * 0.0049),   // 4dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 4,  4, targetDPotRes: 0.5, sgAmpStep: 0.0062, sgAmpStart: 15 * 0.0062),   // 2dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 5,  4, targetDPotRes: 0.5, sgAmpStep: 0.0078, sgAmpStart: 15 * 0.0078),   // 0dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 6,  4, targetDPotRes: 0.5, sgAmpStep: 0.0098, sgAmpStart: 15 * 0.0098),   // -2dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 7,  4, targetDPotRes: 0.5, sgAmpStep: 0.0123, sgAmpStart: 15 * 0.0123),   // -4dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 8,  4, targetDPotRes: 0.5, sgAmpStep: 0.0155, sgAmpStart: 15 * 0.0155),   // -6dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 9,  4, targetDPotRes: 0.5, sgAmpStep: 0.0194, sgAmpStart: 15 * 0.0194),   // -8dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 10, 4, targetDPotRes: 0.5, sgAmpStep: 0.0244, sgAmpStart: 15 * 0.0244)    // -10dB + 8.86dB
    ];

    public ChannelPathConfig[] Channel4PathConfigs { get; set; } = [
        new ChannelPathConfig(PgaPreampGain.High, 0, 12, targetDPotRes: 3, sgAmpStep: 0.0002, sgAmpStart: 15 * 0.0002),    // 30dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 1, 12, targetDPotRes: 2.38, sgAmpStep: 0.0003, sgAmpStart: 15 * 0.0003), // 28dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 2, 12, targetDPotRes: 1.9, sgAmpStep: 0.0004, sgAmpStart: 15 * 0.0004),  // 26dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 3, 12, targetDPotRes: 1.5, sgAmpStep: 0.0005, sgAmpStart: 15 * 0.0005),  // 24dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 4, 12, targetDPotRes: 1.22, sgAmpStep: 0.0006, sgAmpStart: 15 * 0.0006), // 22dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 5, 12, targetDPotRes: 0.98, sgAmpStep: 0.0008, sgAmpStart: 15 * 0.0008), // 20dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 6, 12, targetDPotRes: 0.78, sgAmpStep: 0.0010, sgAmpStart: 15 * 0.0010), // 18dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 7, 12, targetDPotRes: 0.625, sgAmpStep: 0.0012, sgAmpStart: 15 * 0.0012),// 16dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 8, 12, targetDPotRes: 0.5, sgAmpStep: 0.0016, sgAmpStart: 15 * 0.0016),  // 14dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 9,  9, targetDPotRes: 0.5, sgAmpStep: 0.0020, sgAmpStart: 15 * 0.0020),  // 12dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 10, 7, targetDPotRes: 0.5, sgAmpStep: 0.0025, sgAmpStart: 15 * 0.0025),  // 10dB + 8.86dB

        new ChannelPathConfig(PgaPreampGain.Low, 0,  7, targetDPotRes: 0.5, sgAmpStep: 0.0025, sgAmpStart: 15 * 0.0025),   // 10dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 1,  5, targetDPotRes: 0.5, sgAmpStep: 0.0031, sgAmpStart: 15 * 0.0031),   // 8dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 2,  4, targetDPotRes: 0.5, sgAmpStep: 0.0039, sgAmpStart: 15 * 0.0039),   // 6dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 3,  4, targetDPotRes: 0.5, sgAmpStep: 0.0049, sgAmpStart: 15 * 0.0049),   // 4dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 4,  4, targetDPotRes: 0.5, sgAmpStep: 0.0062, sgAmpStart: 15 * 0.0062),   // 2dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 5,  4, targetDPotRes: 0.5, sgAmpStep: 0.0078, sgAmpStart: 15 * 0.0078),   // 0dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 6,  4, targetDPotRes: 0.5, sgAmpStep: 0.0098, sgAmpStart: 15 * 0.0098),   // -2dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 7,  4, targetDPotRes: 0.5, sgAmpStep: 0.0123, sgAmpStart: 15 * 0.0123),   // -4dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 8,  4, targetDPotRes: 0.5, sgAmpStep: 0.0155, sgAmpStart: 15 * 0.0155),   // -6dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 9,  4, targetDPotRes: 0.5, sgAmpStep: 0.0194, sgAmpStart: 15 * 0.0194),   // -8dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 10, 4, targetDPotRes: 0.5, sgAmpStep: 0.0244, sgAmpStart: 15 * 0.0244)    // -10dB + 8.86dB
    ];
}
