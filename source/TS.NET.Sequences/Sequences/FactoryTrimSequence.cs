using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class FactoryTrimSequence : Sequence
{
    public FactoryVariables Variables { get; private set; }

    public FactoryTrimSequence(ModalUiContext modalUiContext, FactoryVariables variables)
    {
        Name = "Factory trim";
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
            new LoadCalibrationFromUserCalStep("Load calibration from device", Variables),
            new Step("Load ADC branch gains")
            {
                Action = (CancellationToken cancellationToken) =>
                {
                    Instruments.Instance.SetThunderscopeBranchGains([0,0,0,0,0,0,0,0]);
                    return Sequencer.Status.Done;
                }
            },
            new WarmupStep("Warmup device", Variables, 40 * 60)
            {
                Skip = true,
                AllowSkip = true
            },

            new TrimStep("Channel 1 - Trim pot", modalUiContext, Variables)
            {
                ChannelIndex = 0,
                ChannelsEnabled = [0],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 10,
                Attenuator = false,
                SampleRateHz = 1_000_000_000,
                BodeFrequencies = [500, 1_000, 2_000, 4_000, 7_000, 10_000, 20_000, 40_000, 70_000, 100_000, 200_000, 400_000, 700_000, 990_000]
            },
            new TrimStep("Channel 2 - Trim pot", modalUiContext, Variables)
            {
                ChannelIndex = 1,
                ChannelsEnabled = [1],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 10,
                Attenuator = false,
                SampleRateHz = 1_000_000_000,
                BodeFrequencies = [500, 1_000, 2_000, 4_000, 7_000, 10_000, 20_000, 40_000, 70_000, 100_000, 200_000, 400_000, 700_000, 990_000]
            },
            new TrimStep("Channel 3 - Trim pot", modalUiContext, Variables)
            {
                ChannelIndex = 2,
                ChannelsEnabled = [2],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 10,
                Attenuator = false,
                SampleRateHz = 1_000_000_000,
                BodeFrequencies = [500, 1_000, 2_000, 4_000, 7_000, 10_000, 20_000, 40_000, 70_000, 100_000, 200_000, 400_000, 700_000, 990_000]
            },
            new TrimStep("Channel 4 - Trim pot", modalUiContext, Variables)
            {
                ChannelIndex = 3,
                ChannelsEnabled = [3],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 10,
                Attenuator = false,
                SampleRateHz = 1_000_000_000,
                BodeFrequencies = [500, 1_000, 2_000, 4_000, 7_000, 10_000, 20_000, 40_000, 70_000, 100_000, 200_000, 400_000, 700_000, 990_000]
            },

            new TrimStep("Channel 1 - Trim cap", modalUiContext, Variables)
            {
                ChannelIndex = 0,
                ChannelsEnabled = [0],
                PgaPreampGain = PgaPreampGain.High,
                PgaLadder = 10,
                Attenuator = true,
                SampleRateHz = 1_000_000_000,
                BodeFrequencies = [5_000, 7_000, 10_000, 20_000, 40_000, 70_000, 100_000, 200_000, 400_000, 700_000, 1_000_000, 2_000_000, 4_000_000, 7_000_000, 10_000_000]
            },
            new TrimStep("Channel 2 - Trim cap", modalUiContext, Variables)
            {
                ChannelIndex = 1,
                ChannelsEnabled = [1],
                PgaPreampGain = PgaPreampGain.High,
                PgaLadder = 10,
                Attenuator = true,
                SampleRateHz = 1_000_000_000,
                BodeFrequencies = [5_000, 7_000, 10_000, 20_000, 40_000, 70_000, 100_000, 200_000, 400_000, 700_000, 1_000_000, 2_000_000, 4_000_000, 7_000_000, 10_000_000]
            },
            new TrimStep("Channel 3 - Trim cap", modalUiContext, Variables)
            {
                ChannelIndex = 2,
                ChannelsEnabled = [2],
                PgaPreampGain = PgaPreampGain.High,
                PgaLadder = 10,
                Attenuator = true,
                SampleRateHz = 1_000_000_000,
                BodeFrequencies = [5_000, 7_000, 10_000, 20_000, 40_000, 70_000, 100_000, 200_000, 400_000, 700_000, 1_000_000, 2_000_000, 4_000_000, 7_000_000, 10_000_000]
            },
            new TrimStep("Channel 4 - Trim cap", modalUiContext, Variables)
            {
                ChannelIndex = 3,
                ChannelsEnabled = [3],
                PgaPreampGain = PgaPreampGain.High,
                PgaLadder = 10,
                Attenuator = true,
                SampleRateHz = 1_000_000_000,
                BodeFrequencies = [5_000, 7_000, 10_000, 20_000, 40_000, 70_000, 100_000, 200_000, 400_000, 700_000, 1_000_000, 2_000_000, 4_000_000, 7_000_000, 10_000_000]
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
