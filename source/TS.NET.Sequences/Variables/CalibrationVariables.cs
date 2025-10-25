namespace TS.NET.Sequences;

public class CalibrationVariables
{
    public string? CalibrationFileName { get; set; } = "thunderscope-calibration.json";
    public int FrontEndSettlingTimeMs { get; set; } = 300;
    public int ParametersSet { get; set; }
    public ThunderscopeCalibrationSettings Calibration { get; set; } = new();
    public DateTimeOffset CalibrationTimestamp { get; set; }

    public ChannelPathConfig[] Channel1PathConfigs { get; set; } = [
    new ChannelPathConfig(PgaPreampGain.High, 0, 54, targetDPotRes: 0.76, sgAmpStep: 0.0002, sgAmpStart: 15 * 0.0002),  // 30dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 1, 54, targetDPotRes: 0.61, sgAmpStep: 0.0003, sgAmpStart: 15 * 0.0003),  // 28dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 2, 54, targetDPotRes: 0.49, sgAmpStep: 0.0004, sgAmpStart: 15 * 0.0004),  // 26dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 3, 54, targetDPotRes: 0.39, sgAmpStep: 0.0005, sgAmpStart: 15 * 0.0005),  // 24dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 4, 54, targetDPotRes: 0.31, sgAmpStep: 0.0006, sgAmpStart: 15 * 0.0006),  // 22dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 5, 53, targetDPotRes: 0.25, sgAmpStep: 0.0008, sgAmpStart: 15 * 0.0008),  // 20dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 6, 42, targetDPotRes: 0.25, sgAmpStep: 0.0010, sgAmpStart: 15 * 0.0010),  // 18dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 7, 33, targetDPotRes: 0.25, sgAmpStep: 0.0012, sgAmpStart: 15 * 0.0012),  // 16dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 8, 26, targetDPotRes: 0.25, sgAmpStep: 0.0016, sgAmpStart: 15 * 0.0016),  // 14dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 9, 20, targetDPotRes: 0.25, sgAmpStep: 0.0020, sgAmpStart: 15 * 0.0020),  // 12dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 10, 16, targetDPotRes: 0.25, sgAmpStep: 0.0025, sgAmpStart: 15 * 0.0025), // 10dB + 8.86dB

        new ChannelPathConfig(PgaPreampGain.Low, 0, 16, targetDPotRes: 0.25, sgAmpStep: 0.0025, sgAmpStart: 15 * 0.0025),   // 10dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 1, 12, targetDPotRes: 0.25, sgAmpStep: 0.0031, sgAmpStart: 15 * 0.0031),   // 8dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 2, 10, targetDPotRes: 0.25, sgAmpStep: 0.0039, sgAmpStart: 15 * 0.0039),   // 6dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 3, 8, targetDPotRes: 0.25, sgAmpStep: 0.0049, sgAmpStart: 15 * 0.0049),    // 4dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 4, 6, targetDPotRes: 0.25, sgAmpStep: 0.0062, sgAmpStart: 15 * 0.0062),    // 2dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 5, 4, targetDPotRes: 0.25, sgAmpStep: 0.0078, sgAmpStart: 15 * 0.0078),    // 0dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 6, 3, targetDPotRes: 0.25, sgAmpStep: 0.0098, sgAmpStart: 15 * 0.0098),    // -2dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 7, 2, targetDPotRes: 0.25, sgAmpStep: 0.0123, sgAmpStart: 15 * 0.0123),    // -4dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 8, 2, targetDPotRes: 0.25, sgAmpStep: 0.0155, sgAmpStart: 15 * 0.0155),    // -6dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 9, 1, targetDPotRes: 0.25, sgAmpStep: 0.0194, sgAmpStart: 15 * 0.0194),    // -8dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 10, 1, targetDPotRes: 0.25, sgAmpStep: 0.0244, sgAmpStart: 15 * 0.0244)    // -10dB + 8.86dB
];

    public ChannelPathConfig[] Channel2PathConfigs { get; set; } = [
        new ChannelPathConfig(PgaPreampGain.High, 0, 54, targetDPotRes: 0.76, sgAmpStep: 0.0002, sgAmpStart: 15 * 0.0002),  // 30dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 1, 54, targetDPotRes: 0.61, sgAmpStep: 0.0003, sgAmpStart: 15 * 0.0003),  // 28dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 2, 54, targetDPotRes: 0.49, sgAmpStep: 0.0004, sgAmpStart: 15 * 0.0004),  // 26dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 3, 54, targetDPotRes: 0.39, sgAmpStep: 0.0005, sgAmpStart: 15 * 0.0005),  // 24dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 4, 54, targetDPotRes: 0.31, sgAmpStep: 0.0006, sgAmpStart: 15 * 0.0006),  // 22dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 5, 53, targetDPotRes: 0.25, sgAmpStep: 0.0008, sgAmpStart: 15 * 0.0008),  // 20dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 6, 42, targetDPotRes: 0.25, sgAmpStep: 0.0010, sgAmpStart: 15 * 0.0010),  // 18dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 7, 33, targetDPotRes: 0.25, sgAmpStep: 0.0012, sgAmpStart: 15 * 0.0012),  // 16dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 8, 26, targetDPotRes: 0.25, sgAmpStep: 0.0016, sgAmpStart: 15 * 0.0016),  // 14dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 9, 20, targetDPotRes: 0.25, sgAmpStep: 0.0020, sgAmpStart: 15 * 0.0020),  // 12dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High, 10, 16, targetDPotRes: 0.25, sgAmpStep: 0.0025, sgAmpStart: 15 * 0.0025), // 10dB + 8.86dB

        new ChannelPathConfig(PgaPreampGain.Low, 0, 16, targetDPotRes: 0.25, sgAmpStep: 0.0025, sgAmpStart: 15 * 0.0025),   // 10dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 1, 12, targetDPotRes: 0.25, sgAmpStep: 0.0031, sgAmpStart: 15 * 0.0031),   // 8dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 2, 10, targetDPotRes: 0.25, sgAmpStep: 0.0039, sgAmpStart: 15 * 0.0039),   // 6dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 3, 8, targetDPotRes: 0.25, sgAmpStep: 0.0049, sgAmpStart: 15 * 0.0049),    // 4dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 4, 6, targetDPotRes: 0.25, sgAmpStep: 0.0062, sgAmpStart: 15 * 0.0062),    // 2dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 5, 4, targetDPotRes: 0.25, sgAmpStep: 0.0078, sgAmpStart: 15 * 0.0078),    // 0dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 6, 3, targetDPotRes: 0.25, sgAmpStep: 0.0098, sgAmpStart: 15 * 0.0098),    // -2dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 7, 2, targetDPotRes: 0.25, sgAmpStep: 0.0123, sgAmpStart: 15 * 0.0123),    // -4dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 8, 2, targetDPotRes: 0.25, sgAmpStep: 0.0155, sgAmpStart: 15 * 0.0155),    // -6dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 9, 1, targetDPotRes: 0.25, sgAmpStep: 0.0194, sgAmpStart: 15 * 0.0194),    // -8dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 10, 1, targetDPotRes: 0.25, sgAmpStep: 0.0244, sgAmpStart: 15 * 0.0244)    // -10dB + 8.86dB
    ];

    public ChannelPathConfig[] Channel3PathConfigs { get; set; } = [
        new ChannelPathConfig(PgaPreampGain.High,0,  54, targetDPotRes: 0.76, sgAmpStep: 0.0002, sgAmpStart: 15 * 0.0002),  // 30dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,1,  54, targetDPotRes: 0.61, sgAmpStep: 0.0003, sgAmpStart: 15 * 0.0003),  // 28dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,2,  54, targetDPotRes: 0.49, sgAmpStep: 0.0004, sgAmpStart: 15 * 0.0004),  // 26dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,3,  54, targetDPotRes: 0.39, sgAmpStep: 0.0005, sgAmpStart: 15 * 0.0005),  // 24dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,4,  54, targetDPotRes: 0.31, sgAmpStep: 0.0006, sgAmpStart: 15 * 0.0006),  // 22dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,5,  53, targetDPotRes: 0.25, sgAmpStep: 0.0008, sgAmpStart: 15 * 0.0008),  // 20dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,6,  42, targetDPotRes: 0.25, sgAmpStep: 0.0010, sgAmpStart: 15 * 0.0010),  // 18dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,7,  33, targetDPotRes: 0.25, sgAmpStep: 0.0012, sgAmpStart: 15 * 0.0012),  // 16dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,8,  26, targetDPotRes: 0.25, sgAmpStep: 0.0016, sgAmpStart: 15 * 0.0016),  // 14dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,9,  20, targetDPotRes: 0.25, sgAmpStep: 0.0020, sgAmpStart: 15 * 0.0020),  // 12dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,10,  16, targetDPotRes: 0.25, sgAmpStep: 0.0025, sgAmpStart: 15 * 0.0025), // 10dB + 8.86dB

        new ChannelPathConfig(PgaPreampGain.Low, 0,  16, targetDPotRes: 0.25, sgAmpStep: 0.0025, sgAmpStart: 15 * 0.0025),  // 10dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 1,  12, targetDPotRes: 0.25, sgAmpStep: 0.0031, sgAmpStart: 15 * 0.0031),  // 8dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 2,  10, targetDPotRes: 0.25, sgAmpStep: 0.0039, sgAmpStart: 15 * 0.0039),  // 6dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 3,  8, targetDPotRes: 0.25, sgAmpStep: 0.0049, sgAmpStart: 15 * 0.0049),   // 4dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 4,  6, targetDPotRes: 0.25, sgAmpStep: 0.0062, sgAmpStart: 15 * 0.0062),   // 2dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 5,  4, targetDPotRes: 0.25, sgAmpStep: 0.0078, sgAmpStart: 15 * 0.0078),   // 0dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 6,  3, targetDPotRes: 0.25, sgAmpStep: 0.0098, sgAmpStart: 15 * 0.0098),   // -2dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 7,  2, targetDPotRes: 0.25, sgAmpStep: 0.0123, sgAmpStart: 15 * 0.0123),   // -4dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 8,  2, targetDPotRes: 0.25, sgAmpStep: 0.0155, sgAmpStart: 15 * 0.0155),   // -6dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 9,  1, targetDPotRes: 0.25, sgAmpStep: 0.0194, sgAmpStart: 15 * 0.0194),   // -8dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 10,  1, targetDPotRes: 0.25, sgAmpStep: 0.0244, sgAmpStart: 15 * 0.0244)   // -10dB + 8.86dB
    ];

    public ChannelPathConfig[] Channel4PathConfigs { get; set; } = [
        new ChannelPathConfig(PgaPreampGain.High,0,  54, targetDPotRes: 0.76, sgAmpStep: 0.0002, sgAmpStart: 15 * 0.0002),  // 30dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,1,  54, targetDPotRes: 0.61, sgAmpStep: 0.0003, sgAmpStart: 15 * 0.0003),  // 28dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,2,  54, targetDPotRes: 0.49, sgAmpStep: 0.0004, sgAmpStart: 15 * 0.0004),  // 26dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,3,  54, targetDPotRes: 0.39, sgAmpStep: 0.0005, sgAmpStart: 15 * 0.0005),  // 24dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,4,  54, targetDPotRes: 0.31, sgAmpStep: 0.0006, sgAmpStart: 15 * 0.0006),  // 22dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,5,  53, targetDPotRes: 0.25, sgAmpStep: 0.0008, sgAmpStart: 15 * 0.0008),  // 20dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,6,  42, targetDPotRes: 0.25, sgAmpStep: 0.0010, sgAmpStart: 15 * 0.0010),  // 18dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,7,  33, targetDPotRes: 0.25, sgAmpStep: 0.0012, sgAmpStart: 15 * 0.0012),  // 16dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,8,  26, targetDPotRes: 0.25, sgAmpStep: 0.0016, sgAmpStart: 15 * 0.0016),  // 14dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,9,  20, targetDPotRes: 0.25, sgAmpStep: 0.0020, sgAmpStart: 15 * 0.0020),  // 12dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.High,10,  16, targetDPotRes: 0.25, sgAmpStep: 0.0025, sgAmpStart: 15 * 0.0025), // 10dB + 8.86dB

        new ChannelPathConfig(PgaPreampGain.Low, 0,  16, targetDPotRes: 0.25, sgAmpStep: 0.0025, sgAmpStart: 15 * 0.0025),  // 10dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 1,  12, targetDPotRes: 0.25, sgAmpStep: 0.0031, sgAmpStart: 15 * 0.0031),  // 8dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 2,  10, targetDPotRes: 0.25, sgAmpStep: 0.0039, sgAmpStart: 15 * 0.0039),  // 6dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 3,  8, targetDPotRes: 0.25, sgAmpStep: 0.0049, sgAmpStart: 15 * 0.0049),   // 4dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 4,  6, targetDPotRes: 0.25, sgAmpStep: 0.0062, sgAmpStart: 15 * 0.0062),   // 2dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 5,  4, targetDPotRes: 0.25, sgAmpStep: 0.0078, sgAmpStart: 15 * 0.0078),   // 0dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 6,  3, targetDPotRes: 0.25, sgAmpStep: 0.0098, sgAmpStart: 15 * 0.0098),   // -2dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 7,  2, targetDPotRes: 0.25, sgAmpStep: 0.0123, sgAmpStart: 15 * 0.0123),   // -4dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 8,  2, targetDPotRes: 0.25, sgAmpStep: 0.0155, sgAmpStart: 15 * 0.0155),   // -6dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 9,  1, targetDPotRes: 0.25, sgAmpStep: 0.0194, sgAmpStart: 15 * 0.0194),   // -8dB + 8.86dB
        new ChannelPathConfig(PgaPreampGain.Low, 10,  1, targetDPotRes: 0.25, sgAmpStep: 0.0244, sgAmpStart: 15 * 0.0244)   // -10dB + 8.86dB
    ];
}
