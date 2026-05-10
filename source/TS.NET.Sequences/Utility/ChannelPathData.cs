namespace TS.NET.Sequences;

public class ChannelPathData
{
    public PgaPreampGain PgaPreampGain { get; set; }
    public byte PgaLadder { get; set; }             // One of 11 values: 0 - 10

    public double Target8bAdcCountPerDacLsb { get; set; }
    public double SigGenAmplitudeStep { get; set; }
    public double SigGenAmplitudeStart { get; set; }

    public ushort TrimDacZeroColdDac { get; set; }
    public double TrimDacZeroColdTemp { get; set; }
    public ushort TrimDacZeroHotDac { get; set; }
    public double TrimDacZeroHotTemp { get; set; }

    public ChannelPathData(PgaPreampGain pgaPreampGain, byte pgaLadder, double targetDPotRes, double sgAmpStep, double sgAmpStart = 0)
    {
        PgaPreampGain = pgaPreampGain;
        PgaLadder = pgaLadder;

        Target8bAdcCountPerDacLsb = targetDPotRes;
        SigGenAmplitudeStep = sgAmpStep;
        SigGenAmplitudeStart = sgAmpStart;

        TrimDacZeroColdDac = 0;
        TrimDacZeroColdTemp = 0;
        TrimDacZeroHotDac = 0;
        TrimDacZeroHotTemp = 0;
    }

    public override string ToString()
    {
        return $"Ladder: {PgaLadder}, Gain: {PgaPreampGain}";
    }
}
