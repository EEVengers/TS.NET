namespace TS.NET.Sequences;

public class ChannelPathConfig
{
    public PgaPreampGain PgaPreampGain { get; set; }
    public byte PgaLadderAttenuator { get; set; }      

    public byte TrimScaleDacInitial { get; set; }
    public double TargetDPotResolution { get; set; }
    public double SigGenAmplitudeStep { get; set; }
    public double SigGenAmplitudeStart { get; set; }

    public ChannelPathConfig(PgaPreampGain pgaPreampGain, byte pgaLadderAttenuator, byte dpot, double targetDPotRes, double sgAmpStep, double sgAmpStart = 0)
    {
        PgaPreampGain = pgaPreampGain;
        PgaLadderAttenuator = pgaLadderAttenuator;

        TrimScaleDacInitial = dpot;
        TargetDPotResolution = targetDPotRes;
        SigGenAmplitudeStep = sgAmpStep;
        SigGenAmplitudeStart = sgAmpStart;
    }

    public override string ToString()
    {
        return $"Ladder: {PgaLadderAttenuator}, Gain: {PgaPreampGain}";
    }
}
