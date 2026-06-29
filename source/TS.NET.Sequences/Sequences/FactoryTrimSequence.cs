using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class FactoryTrimArbProfile
{
    public required string Name { get; set; }
    public required double[] Frequencies { get; set; }
    public required int CyclesPerFrequency = 3;
    public required uint SampleRateHz { get; set; }
    public int Length { get; set; }
}

public class FactoryTrimSequence : Sequence
{
    public FactoryVariables Variables { get; private set; }

    private FactoryTrimArbProfile trimPotArbWaveform = new()
    {
        Name = "factory-trimpot",
        Frequencies = [100, 400, 1_000, 2_000, 3_000, 4_000, 6_000, 8_000, 10_000, 20_000, 30_000, 40_000, 60_000, 80_000, 100_000, 200_000, 300_000, 400_000, 600_000, 800_000, 1_000_000],
        CyclesPerFrequency = 3,
        SampleRateHz = 75_000_000
    };
    private FactoryTrimArbProfile trimCapArbWaveform = new()
    {
        Name = "factory-trimcap",
        Frequencies = [4_000, 6_000, 8_000, 10_000, 20_000, 40_000, 60_000, 80_000, 100_000, 200_000, 400_000, 600_000, 800_000, 1_000_000, 2_000_000, 4_000_000],
        CyclesPerFrequency = 3,
        SampleRateHz = 75_000_000
    };

    public FactoryTrimSequence(ModalUiContext modalUiContext, FactoryVariables variables)
    {
        Name = "Factory trim";
        Version = "1.0";
        Variables = variables;
        AddSteps(modalUiContext);
        SetStepIndices();
    }

    private void AddSteps(ModalUiContext modalUiContext)
    {
        Steps =
        [
            new ModalDialogStep("Cable check", modalUiContext)
            {
                Title = "Cable check",
                Message = "Are cables connected from 2x signal generators to channel 1-4?",
                Buttons = DialogButtons.YesNo,
                Icon = DialogIcon.Question
            },
            new InitialiseDeviceStep("Initialise device", Variables),
            new InitialiseSigGensStep("Initialise signal generators", Variables),

            new WarmupStep("Warmup device", Variables, 40 * 60)
            {
                Skip = true,
                AllowSkip = true
            },

            // LG L7 used for AttenuatorStep
            new TrimDacZeroHotStep("Channel 1 - Trim DAC zero - LG L10", channelIndex: 0, PgaPreampGain.Low, pgaLadder: 10, Variables),
            new TrimDacZeroHotStep("Channel 1 - Trim DAC zero - LG L7", channelIndex: 0, PgaPreampGain.Low, pgaLadder: 7, Variables),
            new TrimDacZeroHotStep("Channel 1 - Trim DAC zero - LG L1", channelIndex: 0, PgaPreampGain.Low, pgaLadder: 1, Variables) { PostAction = cancellationToken => { Variables.LastDacValue = 2048; } },
            new TrimDacZeroHotStep("Channel 2 - Trim DAC zero - LG L10", channelIndex: 1, PgaPreampGain.Low, pgaLadder: 10, Variables),
            new TrimDacZeroHotStep("Channel 2 - Trim DAC zero - LG L7", channelIndex: 1, PgaPreampGain.Low, pgaLadder: 7, Variables),
            new TrimDacZeroHotStep("Channel 2 - Trim DAC zero - LG L1", channelIndex: 1, PgaPreampGain.Low, pgaLadder: 1, Variables) { PostAction = cancellationToken => { Variables.LastDacValue = 2048; } },
            new TrimDacZeroHotStep("Channel 3 - Trim DAC zero - LG L10", channelIndex: 2, PgaPreampGain.Low, pgaLadder: 10, Variables),
            new TrimDacZeroHotStep("Channel 3 - Trim DAC zero - LG L7", channelIndex: 2, PgaPreampGain.Low, pgaLadder: 7, Variables),
            new TrimDacZeroHotStep("Channel 3 - Trim DAC zero - LG L1", channelIndex: 2, PgaPreampGain.Low, pgaLadder: 1, Variables) { PostAction = cancellationToken => { Variables.LastDacValue = 2048; } },
            new TrimDacZeroHotStep("Channel 4 - Trim DAC zero - LG L10", channelIndex: 3, PgaPreampGain.Low, pgaLadder: 10, Variables),
            new TrimDacZeroHotStep("Channel 4 - Trim DAC zero - LG L7", channelIndex: 3, PgaPreampGain.Low, pgaLadder: 7, Variables),
            new TrimDacZeroHotStep("Channel 4 - Trim DAC zero - LG L1", channelIndex: 3, PgaPreampGain.Low, pgaLadder: 1, Variables) { PostAction = cancellationToken => { Variables.LastDacValue = 2048; Variables.TrimDacZeroCalibrated = true; } },
            
            // LG L7 used for AttenuatorStep
            new BufferInputVppStep("Channel 1 - Buffer input Vpp - LG L10", channelIndex: 0, PgaPreampGain.Low, pgaLadder: 10, Variables),
            new BufferInputVppStep("Channel 2 - Buffer input Vpp - LG L10", channelIndex: 1, PgaPreampGain.Low, pgaLadder: 10, Variables),
            new BufferInputVppStep("Channel 3 - Buffer input Vpp - LG L10", channelIndex: 2, PgaPreampGain.Low, pgaLadder: 10, Variables),
            new BufferInputVppStep("Channel 4 - Buffer input Vpp - LG L10", channelIndex: 3, PgaPreampGain.Low, pgaLadder: 10, Variables),
            new BufferInputVppStep("Channel 1 - Buffer input Vpp - LG L7", channelIndex: 0, PgaPreampGain.Low, pgaLadder: 7, Variables),
            new BufferInputVppStep("Channel 2 - Buffer input Vpp - LG L7", channelIndex: 1, PgaPreampGain.Low, pgaLadder: 7, Variables),
            new BufferInputVppStep("Channel 3 - Buffer input Vpp - LG L7", channelIndex: 2, PgaPreampGain.Low, pgaLadder: 7, Variables),
            new BufferInputVppStep("Channel 4 - Buffer input Vpp - LG L7", channelIndex: 3, PgaPreampGain.Low, pgaLadder: 7, Variables),
            new BufferInputVppStep("Channel 1 - Buffer input Vpp - LG L1", channelIndex: 0, PgaPreampGain.Low, pgaLadder: 1, Variables),
            new BufferInputVppStep("Channel 2 - Buffer input Vpp - LG L1", channelIndex: 1, PgaPreampGain.Low, pgaLadder: 1, Variables),
            new BufferInputVppStep("Channel 3 - Buffer input Vpp - LG L1", channelIndex: 2, PgaPreampGain.Low, pgaLadder: 1, Variables),
            new BufferInputVppStep("Channel 4 - Buffer input Vpp - LG L1", channelIndex: 3, PgaPreampGain.Low, pgaLadder: 1, Variables),
            
            new AttenuatorStep("Channel 1 - Attenuator scale", channelIndex: 0, Variables) { IgnoreError = true },
            new AttenuatorStep("Channel 2 - Attenuator scale", channelIndex: 1, Variables) { IgnoreError = true },
            new AttenuatorStep("Channel 3 - Attenuator scale", channelIndex: 2, Variables) { IgnoreError = true },
            new AttenuatorStep("Channel 4 - Attenuator scale", channelIndex: 3, Variables) { IgnoreError = true },

            new Step("Load waveforms to signal generators")
            {
                Action = (CancellationToken cancellationToken) =>
                {
                    trimPotArbWaveform.Length = SigGens.Instance.LoadSdgArbitraryBurstList(0, trimPotArbWaveform.SampleRateHz, trimPotArbWaveform.Frequencies, trimPotArbWaveform.CyclesPerFrequency, waveName: trimPotArbWaveform.Name, waveShape: SigGens.ArbitraryWaveShape.Square);
                    SigGens.Instance.LoadSdgArbitraryBurstList(2, trimPotArbWaveform.SampleRateHz, trimPotArbWaveform.Frequencies, trimPotArbWaveform.CyclesPerFrequency, waveName: trimPotArbWaveform.Name, waveShape: SigGens.ArbitraryWaveShape.Square);
                    
                    trimCapArbWaveform.Length = SigGens.Instance.LoadSdgArbitraryBurstList(0, trimCapArbWaveform.SampleRateHz, trimCapArbWaveform.Frequencies, trimCapArbWaveform.CyclesPerFrequency, waveName: trimCapArbWaveform.Name, waveShape: SigGens.ArbitraryWaveShape.Square);
                    SigGens.Instance.LoadSdgArbitraryBurstList(2, trimCapArbWaveform.SampleRateHz, trimCapArbWaveform.Frequencies, trimCapArbWaveform.CyclesPerFrequency, waveName: trimCapArbWaveform.Name, waveShape: SigGens.ArbitraryWaveShape.Square);
                    
                    return Sequencer.Status.Done;
                }
            },

            new TrimStep2("Channel 1 - Trim pot", modalUiContext, Variables)
            {
                ChannelIndex = 0,
                ChannelsEnabled = [0],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 10,
                Attenuator = false,
                SampleRateHz = 100_000_000,
                ArbProfile = trimPotArbWaveform
            },
            new TrimStep2("Channel 2 - Trim pot", modalUiContext, Variables)
            {
                ChannelIndex = 1,
                ChannelsEnabled = [1],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 10,
                Attenuator = false,
                SampleRateHz = 100_000_000,
                ArbProfile = trimPotArbWaveform
            },
            new TrimStep2("Channel 3 - Trim pot", modalUiContext, Variables)
            {
                ChannelIndex = 2,
                ChannelsEnabled = [2],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 10,
                Attenuator = false,
                SampleRateHz = 100_000_000,
                ArbProfile = trimPotArbWaveform
            },
            new TrimStep2("Channel 4 - Trim pot", modalUiContext, Variables)
            {
                ChannelIndex = 3,
                ChannelsEnabled = [3],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 10,
                Attenuator = false,
                SampleRateHz = 100_000_000,
                ArbProfile = trimPotArbWaveform
            },

            new TrimStep2("Channel 1 - Trim cap", modalUiContext, Variables)
            {
                ChannelIndex = 0,
                ChannelsEnabled = [0],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 1,
                Attenuator = true,
                SampleRateHz = 100_000_000,
                ArbProfile = trimCapArbWaveform
            },
            new TrimStep2("Channel 2 - Trim cap", modalUiContext, Variables)
            {
                ChannelIndex = 1,
                ChannelsEnabled = [1],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 1,
                Attenuator = true,
                SampleRateHz = 100_000_000,
                ArbProfile = trimCapArbWaveform
            },
            new TrimStep2("Channel 3 - Trim cap", modalUiContext, Variables)
            {
                ChannelIndex = 2,
                ChannelsEnabled = [2],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 1,
                Attenuator = true,
                SampleRateHz = 100_000_000,
                ArbProfile = trimCapArbWaveform
            },
            new TrimStep2("Channel 4 - Trim cap", modalUiContext, Variables)
            {
                ChannelIndex = 3,
                ChannelsEnabled = [3],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 1,
                Attenuator = true,
                SampleRateHz = 100_000_000,
                ArbProfile = trimCapArbWaveform
            },

            new Step("Cleanup")
            {
                Action = (CancellationToken cancellationToken) =>
                {
                    Instruments.Instance.Close();
                    SigGens.Instance.Close();
                    return Sequencer.Status.Done;
                }
            },
        ];
    }
}
