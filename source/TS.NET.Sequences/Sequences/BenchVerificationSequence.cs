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

            new BodePlotStep("Channel 1 - 50R, attenuator off", 0, 21, 0.8 * 2, false, Variables),
            new BodePlotStep("Channel 1 - 50R, attenuator on", 0, 18, 10 * 2, true, Variables),
            //new BodePlotStep("Channel 2 - 50R", 1, 21, 1.6, false, Variables),
            //new BodePlotStep("Channel 3 - 50R", 2, 21, 1.6, false, Variables),
            //new BodePlotStep("Channel 4 - 50R", 3, 21, 1.6, false, Variables),

            new Step("Disconnect signal generator"){ Action = (CancellationToken cancellationToken) => { Instruments.Instance.SetSdgChannel(-1); return Sequencer.Status.Done; }},

            new Step("Cleanup"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.Close();
                return Sequencer.Status.Done;
            }},
        ];
    }
}
