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

            // Note: due to 5Vpp SDG limit for frequencies above 10 MHz, the attenuator on steps can only go from HG L0 to HG L10

            new BodePlotStep("Channel 1 - 1 GSPS, 50R, attenuator off, LG L10", channelIndex: 0, [0], 1_000_000_000, preamp: PgaPreampGain.Low, ladder: 10, attenuator: false, Variables),
            new BodePlotStep("Channel 1 - 1 GSPS, 50R, attenuator on, HG L10", channelIndex: 0, [0], 1_000_000_000, preamp: PgaPreampGain.High, ladder: 10, attenuator: true, Variables),

            new BodePlotStep("Channel 2 - 1 GSPS, 50R, attenuator off, LG L10", channelIndex: 1, [1], 1_000_000_000, preamp: PgaPreampGain.Low, ladder: 10, attenuator: false, Variables),
            new BodePlotStep("Channel 2 - 1 GSPS, 50R, attenuator on, HG L10", channelIndex: 1, [1], 1_000_000_000, preamp: PgaPreampGain.High, ladder: 10, attenuator: true, Variables),

            new BodePlotStep("Channel 3 - 1 GSPS, 50R, attenuator off, LG L10", channelIndex: 2, [2], 1_000_000_000, preamp: PgaPreampGain.Low, ladder: 10, attenuator: false, Variables),
            new BodePlotStep("Channel 3 - 1 GSPS, 50R, attenuator on, HG L10", channelIndex: 2, [2], 1_000_000_000, preamp: PgaPreampGain.High, ladder: 10, attenuator: true, Variables),

            new BodePlotStep("Channel 4 - 1 GSPS, 50R, attenuator off, LG L10", channelIndex: 3, [3], 1_000_000_000, preamp: PgaPreampGain.Low, ladder: 10, attenuator: false, Variables),
            new BodePlotStep("Channel 4 - 1 GSPS, 50R, attenuator on, HG L10", channelIndex: 3, [3], 1_000_000_000, preamp: PgaPreampGain.High, ladder: 10, attenuator: true, Variables),

            new Step("Disconnect signal generator"){ Action = (CancellationToken cancellationToken) => { SigGens.Instance.SetSdgChannel([]); return Sequencer.Status.Done; }},

            new Step("Cleanup"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.Close();
                return Sequencer.Status.Done;
            }},
        ];
    }
}
