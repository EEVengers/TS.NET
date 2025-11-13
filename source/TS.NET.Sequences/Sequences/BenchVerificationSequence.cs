using TS.NET.Photino;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class BenchVerificationSequence : Sequence
{
    public BenchVerificationVariables Variables { get; private set; }

    public BenchVerificationSequence(Func<Dialog, DialogResult> uiDialog, BenchVerificationVariables variables)
    {
        Name = "Bench verification";
        Variables = variables;
        AddSteps(uiDialog);
        SetStepIndices();
    }

    private void AddSteps(Func<Dialog, DialogResult> uiDialog)
    {
        Steps =
        [
            new DialogStep("Cable check", uiDialog){ Title = "Cable check", Text = "Cables connected from 2x signal generators to channels 1-4?", Buttons = DialogButtons.YesNo, Icon = DialogIcon.Question },
            new InitialiseDeviceStep("Initialise device", Variables),
            new InitialiseSigGensStep("Initialise instruments", Variables),
            new LoadUserCalFromDeviceStep("Load calibration from device", Variables),
            new WarmupStep("Warmup device", Variables) { Skip = false, AllowSkip = true },

            new AdcBranchGainsStep("Channel 1 - ADC branch gains", 0, 1_000_000_000, Variables),
            new BodePlotStep("Channel 1 - 1 GSPS, 50R, 0.8 Vpp, attenuator off, LG L10", channelIndex: 0, preamp: PgaPreampGain.Low, ladder: 10, 0.8 * 2, false, Variables),
            new BodePlotStep("Channel 1 - 1 GSPS, 50R, 10 Vpp, attenuator on, LG L7", channelIndex: 0, preamp: PgaPreampGain.Low, ladder: 7, 10 * 2, true, Variables),

            new AdcBranchGainsStep("Channel 2 - ADC branch gains", 1, 1_000_000_000, Variables),
            new BodePlotStep("Channel 2 - 1 GSPS, 50R, 0.8 Vpp, attenuator off, LG L10", channelIndex: 1, preamp: PgaPreampGain.Low, ladder: 10, 0.8 * 2, false, Variables),
            new BodePlotStep("Channel 2 - 1 GSPS, 50R, 10 Vpp, attenuator on, LG L7", channelIndex: 1, preamp: PgaPreampGain.Low, ladder: 7, 10 * 2, true, Variables),

            new AdcBranchGainsStep("Channel 3 - ADC branch gains", 2, 1_000_000_000, Variables),
            new BodePlotStep("Channel 3 - 1 GSPS, 50R, 0.8 Vpp, attenuator off, LG L10", channelIndex: 2, preamp: PgaPreampGain.Low, ladder: 10, 0.8 * 2, false, Variables),
            new BodePlotStep("Channel 3 - 1 GSPS, 50R, 10 Vpp, attenuator on, LG L7", channelIndex: 2, preamp: PgaPreampGain.Low, ladder: 7, 10 * 2, true, Variables),

            new AdcBranchGainsStep("Channel 4 - ADC branch gains", 3, 1_000_000_000, Variables),
            new BodePlotStep("Channel 4 - 1 GSPS, 50R, 0.8 Vpp, attenuator off, LG L10", channelIndex: 3, preamp: PgaPreampGain.Low, ladder: 10, 0.8 * 2, false, Variables),
            new BodePlotStep("Channel 4 - 1 GSPS, 50R, 10 Vpp, attenuator on, LG L7", channelIndex: 3, preamp: PgaPreampGain.Low, ladder: 7, 10 * 2, true, Variables),

            new Step("Disconnect signal generator"){ Action = (CancellationToken cancellationToken) => { Instruments.Instance.SetSdgChannel(-1); return Sequencer.Status.Done; }},

            new Step("Cleanup"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.Close();
                return Sequencer.Status.Done;
            }},
        ];
    }
}
