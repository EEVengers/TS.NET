namespace TS.NET.Sequences;

public class CalibrationVariables : CommonVariables
{
    public string? CalibrationFileName { get; set; } = "thunderscope-calibration.json";
    public int ParametersSet { get; set; }
    public DateTimeOffset CalibrationTimestamp { get; set; }
    public ushort LastDacValue { get; set; } = 2048;

    public bool TrimDacZeroCalibrated { get; set; } = false;
    public bool TrimDacScaleCalibrated { get; set; } = false;
    public bool BufferInputVppCalibrated { get; set; } = false;

    // Lowest dpot value is 4, which gives around 1.9V to 3.1V at the PGA negative input - the allowable CM range.
    // The ramp up of targetDPotRes on the highest gain configs is to give good range with the attenuator on.
    // The targetDPotRes still tends to be above noise floor of the frontend so it balances out well.
    // targetDPotRes of 3 is about vertical 80 divisions (10 minor divisions within 8 major divisions)
    public ChannelPathData[] Channel1PathConfigs { get; set; } = [
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 0, targetDPotRes: 1.9, sgAmpStep: 0.01 * 0.05, sgAmpStart: 0.01 * 0.75),      // 30dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 1, targetDPotRes: 1.5, sgAmpStep: 0.013 * 0.05, sgAmpStart: 0.013 * 0.75),    // 28dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 2, targetDPotRes: 1.22, sgAmpStep: 0.016 * 0.05, sgAmpStart: 0.016 * 0.75),   // 26dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 3, targetDPotRes: 0.98, sgAmpStep: 0.020 * 0.05, sgAmpStart: 0.020 * 0.75),   // 24dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 4, targetDPotRes: 0.78, sgAmpStep: 0.025 * 0.05, sgAmpStart: 0.025 * 0.75),   // 22dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 5, targetDPotRes: 0.625, sgAmpStep: 0.032 * 0.05, sgAmpStart: 0.032 * 0.75),  // 20dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 6, targetDPotRes: 0.5, sgAmpStep: 0.040 * 0.05, sgAmpStart: 0.040 * 0.75),    // 18dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 7, targetDPotRes: 0.5, sgAmpStep: 0.050 * 0.05, sgAmpStart: 0.050 * 0.75),    // 16dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 8, targetDPotRes: 0.5, sgAmpStep: 0.063 * 0.05, sgAmpStart: 0.063 * 0.75),    // 14dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 9, targetDPotRes: 0.5, sgAmpStep: 0.080 * 0.05, sgAmpStart: 0.080 * 0.75),    // 12dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 10, targetDPotRes: 0.5, sgAmpStep: 0.100 * 0.05, sgAmpStart: 0.100 * 0.75),   // 10dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 0, targetDPotRes: 0.5, sgAmpStep: 0.100 * 0.05, sgAmpStart: 0.100 * 0.75),     // 10dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 1, targetDPotRes: 0.5, sgAmpStep: 0.126 * 0.05, sgAmpStart: 0.126 * 0.75),     // 8dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 2, targetDPotRes: 0.5, sgAmpStep: 0.159 * 0.05, sgAmpStart: 0.159 * 0.75),     // 6dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 3, targetDPotRes: 0.5, sgAmpStep: 0.200 * 0.05, sgAmpStart: 0.200 * 0.75),     // 4dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 4, targetDPotRes: 0.5, sgAmpStep: 0.250 * 0.05, sgAmpStart: 0.250 * 0.75),     // 2dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 5, targetDPotRes: 0.5, sgAmpStep: 0.316 * 0.05, sgAmpStart: 0.316 * 0.75),     // 0dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 6, targetDPotRes: 0.5, sgAmpStep: 0.397 * 0.05, sgAmpStart: 0.397 * 0.75),     // -2dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 7, targetDPotRes: 0.5, sgAmpStep: 0.500 * 0.05, sgAmpStart: 0.500 * 0.75),     // -4dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 8, targetDPotRes: 0.5, sgAmpStep: 0.627 * 0.05, sgAmpStart: 0.627 * 0.75),     // -6dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 9, targetDPotRes: 0.5, sgAmpStep: 0.789 * 0.05, sgAmpStart: 0.789 * 0.75),     // -8dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 10, targetDPotRes: 0.5, sgAmpStep: 0.994 * 0.05, sgAmpStart: 0.994 * 0.75)     // -10dB + 8.86dB
];

    public ChannelPathData[] Channel2PathConfigs { get; set; } = [
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 0, targetDPotRes: 1.9, sgAmpStep: 0.01 * 0.05, sgAmpStart: 0.01 * 0.75),      // 30dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 1, targetDPotRes: 1.5, sgAmpStep: 0.013 * 0.05, sgAmpStart: 0.013 * 0.75),    // 28dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 2, targetDPotRes: 1.22, sgAmpStep: 0.016 * 0.05, sgAmpStart: 0.016 * 0.75),   // 26dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 3, targetDPotRes: 0.98, sgAmpStep: 0.020 * 0.05, sgAmpStart: 0.020 * 0.75),   // 24dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 4, targetDPotRes: 0.78, sgAmpStep: 0.025 * 0.05, sgAmpStart: 0.025 * 0.75),   // 22dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 5, targetDPotRes: 0.625, sgAmpStep: 0.032 * 0.05, sgAmpStart: 0.032 * 0.75),  // 20dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 6, targetDPotRes: 0.5, sgAmpStep: 0.040 * 0.05, sgAmpStart: 0.040 * 0.75),    // 18dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 7, targetDPotRes: 0.5, sgAmpStep: 0.050 * 0.05, sgAmpStart: 0.050 * 0.75),    // 16dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 8, targetDPotRes: 0.5, sgAmpStep: 0.063 * 0.05, sgAmpStart: 0.063 * 0.75),    // 14dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 9, targetDPotRes: 0.5, sgAmpStep: 0.080 * 0.05, sgAmpStart: 0.080 * 0.75),    // 12dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 10, targetDPotRes: 0.5, sgAmpStep: 0.100 * 0.05, sgAmpStart: 0.100 * 0.75),   // 10dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 0, targetDPotRes: 0.5, sgAmpStep: 0.100 * 0.05, sgAmpStart: 0.100 * 0.75),     // 10dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 1, targetDPotRes: 0.5, sgAmpStep: 0.126 * 0.05, sgAmpStart: 0.126 * 0.75),     // 8dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 2, targetDPotRes: 0.5, sgAmpStep: 0.159 * 0.05, sgAmpStart: 0.159 * 0.75),     // 6dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 3, targetDPotRes: 0.5, sgAmpStep: 0.200 * 0.05, sgAmpStart: 0.200 * 0.75),     // 4dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 4, targetDPotRes: 0.5, sgAmpStep: 0.250 * 0.05, sgAmpStart: 0.250 * 0.75),     // 2dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 5, targetDPotRes: 0.5, sgAmpStep: 0.316 * 0.05, sgAmpStart: 0.316 * 0.75),     // 0dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 6, targetDPotRes: 0.5, sgAmpStep: 0.397 * 0.05, sgAmpStart: 0.397 * 0.75),     // -2dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 7, targetDPotRes: 0.5, sgAmpStep: 0.500 * 0.05, sgAmpStart: 0.500 * 0.75),     // -4dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 8, targetDPotRes: 0.5, sgAmpStep: 0.627 * 0.05, sgAmpStart: 0.627 * 0.75),     // -6dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 9, targetDPotRes: 0.5, sgAmpStep: 0.789 * 0.05, sgAmpStart: 0.789 * 0.75),     // -8dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 10, targetDPotRes: 0.5, sgAmpStep: 0.994 * 0.05, sgAmpStart: 0.994 * 0.75)     // -10dB + 8.86dB
    ];

    public ChannelPathData[] Channel3PathConfigs { get; set; } = [
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 0, targetDPotRes: 1.9, sgAmpStep: 0.01 * 0.05, sgAmpStart: 0.01 * 0.75),      // 30dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 1, targetDPotRes: 1.5, sgAmpStep: 0.013 * 0.05, sgAmpStart: 0.013 * 0.75),    // 28dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 2, targetDPotRes: 1.22, sgAmpStep: 0.016 * 0.05, sgAmpStart: 0.016 * 0.75),   // 26dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 3, targetDPotRes: 0.98, sgAmpStep: 0.020 * 0.05, sgAmpStart: 0.020 * 0.75),   // 24dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 4, targetDPotRes: 0.78, sgAmpStep: 0.025 * 0.05, sgAmpStart: 0.025 * 0.75),   // 22dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 5, targetDPotRes: 0.625, sgAmpStep: 0.032 * 0.05, sgAmpStart: 0.032 * 0.75),  // 20dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 6, targetDPotRes: 0.5, sgAmpStep: 0.040 * 0.05, sgAmpStart: 0.040 * 0.75),    // 18dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 7, targetDPotRes: 0.5, sgAmpStep: 0.050 * 0.05, sgAmpStart: 0.050 * 0.75),    // 16dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 8, targetDPotRes: 0.5, sgAmpStep: 0.063 * 0.05, sgAmpStart: 0.063 * 0.75),    // 14dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 9, targetDPotRes: 0.5, sgAmpStep: 0.080 * 0.05, sgAmpStart: 0.080 * 0.75),    // 12dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 10, targetDPotRes: 0.5, sgAmpStep: 0.100 * 0.05, sgAmpStart: 0.100 * 0.75),   // 10dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 0, targetDPotRes: 0.5, sgAmpStep: 0.100 * 0.05, sgAmpStart: 0.100 * 0.75),     // 10dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 1, targetDPotRes: 0.5, sgAmpStep: 0.126 * 0.05, sgAmpStart: 0.126 * 0.75),     // 8dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 2, targetDPotRes: 0.5, sgAmpStep: 0.159 * 0.05, sgAmpStart: 0.159 * 0.75),     // 6dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 3, targetDPotRes: 0.5, sgAmpStep: 0.200 * 0.05, sgAmpStart: 0.200 * 0.75),     // 4dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 4, targetDPotRes: 0.5, sgAmpStep: 0.250 * 0.05, sgAmpStart: 0.250 * 0.75),     // 2dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 5, targetDPotRes: 0.5, sgAmpStep: 0.316 * 0.05, sgAmpStart: 0.316 * 0.75),     // 0dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 6, targetDPotRes: 0.5, sgAmpStep: 0.397 * 0.05, sgAmpStart: 0.397 * 0.75),     // -2dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 7, targetDPotRes: 0.5, sgAmpStep: 0.500 * 0.05, sgAmpStart: 0.500 * 0.75),     // -4dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 8, targetDPotRes: 0.5, sgAmpStep: 0.627 * 0.05, sgAmpStart: 0.627 * 0.75),     // -6dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 9, targetDPotRes: 0.5, sgAmpStep: 0.789 * 0.05, sgAmpStart: 0.789 * 0.75),     // -8dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 10, targetDPotRes: 0.5, sgAmpStep: 0.994 * 0.05, sgAmpStart: 0.994 * 0.75)     // -10dB + 8.86dB
    ];

    public ChannelPathData[] Channel4PathConfigs { get; set; } = [
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 0, targetDPotRes: 1.9, sgAmpStep: 0.01 * 0.05, sgAmpStart: 0.01 * 0.75),      // 30dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 1, targetDPotRes: 1.5, sgAmpStep: 0.013 * 0.05, sgAmpStart: 0.013 * 0.75),    // 28dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 2, targetDPotRes: 1.22, sgAmpStep: 0.016 * 0.05, sgAmpStart: 0.016 * 0.75),   // 26dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 3, targetDPotRes: 0.98, sgAmpStep: 0.020 * 0.05, sgAmpStart: 0.020 * 0.75),   // 24dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 4, targetDPotRes: 0.78, sgAmpStep: 0.025 * 0.05, sgAmpStart: 0.025 * 0.75),   // 22dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 5, targetDPotRes: 0.625, sgAmpStep: 0.032 * 0.05, sgAmpStart: 0.032 * 0.75),  // 20dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 6, targetDPotRes: 0.5, sgAmpStep: 0.040 * 0.05, sgAmpStart: 0.040 * 0.75),    // 18dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 7, targetDPotRes: 0.5, sgAmpStep: 0.050 * 0.05, sgAmpStart: 0.050 * 0.75),    // 16dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 8, targetDPotRes: 0.5, sgAmpStep: 0.063 * 0.05, sgAmpStart: 0.063 * 0.75),    // 14dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 9, targetDPotRes: 0.5, sgAmpStep: 0.080 * 0.05, sgAmpStart: 0.080 * 0.75),    // 12dB + 8.86dB
        new ChannelPathData(PgaPreampGain.High, pgaLadder: 10, targetDPotRes: 0.5, sgAmpStep: 0.100 * 0.05, sgAmpStart: 0.100 * 0.75),   // 10dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 0, targetDPotRes: 0.5, sgAmpStep: 0.100 * 0.05, sgAmpStart: 0.100 * 0.75),     // 10dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 1, targetDPotRes: 0.5, sgAmpStep: 0.126 * 0.05, sgAmpStart: 0.126 * 0.75),     // 8dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 2, targetDPotRes: 0.5, sgAmpStep: 0.159 * 0.05, sgAmpStart: 0.159 * 0.75),     // 6dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 3, targetDPotRes: 0.5, sgAmpStep: 0.200 * 0.05, sgAmpStart: 0.200 * 0.75),     // 4dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 4, targetDPotRes: 0.5, sgAmpStep: 0.250 * 0.05, sgAmpStart: 0.250 * 0.75),     // 2dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 5, targetDPotRes: 0.5, sgAmpStep: 0.316 * 0.05, sgAmpStart: 0.316 * 0.75),     // 0dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 6, targetDPotRes: 0.5, sgAmpStep: 0.397 * 0.05, sgAmpStart: 0.397 * 0.75),     // -2dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 7, targetDPotRes: 0.5, sgAmpStep: 0.500 * 0.05, sgAmpStart: 0.500 * 0.75),     // -4dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 8, targetDPotRes: 0.5, sgAmpStep: 0.627 * 0.05, sgAmpStart: 0.627 * 0.75),     // -6dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 9, targetDPotRes: 0.5, sgAmpStep: 0.789 * 0.05, sgAmpStart: 0.789 * 0.75),     // -8dB + 8.86dB
        new ChannelPathData(PgaPreampGain.Low, pgaLadder: 10, targetDPotRes: 0.5, sgAmpStep: 0.994 * 0.05, sgAmpStart: 0.994 * 0.75)     // -10dB + 8.86dB
    ];
}