using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class FactoryTrimSequence : Sequence
{
    public BenchVerificationVariables Variables { get; private set; }

    public FactoryTrimSequence(ModalUiContext modalUiContext, BenchVerificationVariables variables)
    {
        Name = "Bench verification";
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
            new LoadUserCalFromDeviceStep("Load calibration from device", Variables),
            new Step("Load ADC branch gains")
            {
                Action = (CancellationToken cancellationToken) =>
                {
                    Instruments.Instance.SetThunderscopeAdcCalibration(Variables.Calibration.Adc.ToDriver());
                    return Sequencer.Status.Done;
                }
            },
            new WarmupStep("Warmup device", Variables)
            {
                Skip = false,
                AllowSkip = true
            },

            new TrimPotStep("Channel 1 - Trim pot", modalUiContext, Variables)
            {
                ChannelIndex = 0,
                ChannelsEnabled = [0],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 10,
                Attenuator = false,
                SampleRateHz = 1_000_000_000
            },
            new TrimPotStep("Channel 2 - Trim pot", modalUiContext, Variables)
            {
                ChannelIndex = 1,
                ChannelsEnabled = [1],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 10,
                Attenuator = false,
                SampleRateHz = 1_000_000_000
            },
            new TrimPotStep("Channel 3 - Trim pot", modalUiContext, Variables)
            {
                ChannelIndex = 2,
                ChannelsEnabled = [2],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 10,
                Attenuator = false,
                SampleRateHz = 1_000_000_000
            },
            new TrimPotStep("Channel 4 - Trim pot", modalUiContext, Variables)
            {
                ChannelIndex = 3,
                ChannelsEnabled = [3],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 10,
                Attenuator = false,
                SampleRateHz = 1_000_000_000
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
