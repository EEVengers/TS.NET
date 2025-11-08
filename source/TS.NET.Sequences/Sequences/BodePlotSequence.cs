using TS.NET.Photino;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class BodePlotSequence : Sequence
{
    public BodePlotVariables Variables { get; private set; }

    public BodePlotSequence(Func<Dialog, DialogResult> uiDialog, BodePlotVariables variables)
    {
        Name = "Bode plot";
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
            //new WarmupStep("Warmup device", Variables) { Skip = false },

            new Step("Connect SDG2042X"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetSdgChannel(0);
                cancellationToken.WaitHandle.WaitOne(2000);
                return Sequencer.Status.Done;
            }},

            new BodePlotStep("Channel 1", 0, 21, 0.8, false, Variables),

            new Step("Connect SDG2042X"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetSdgChannel(1);
                cancellationToken.WaitHandle.WaitOne(2000);
                return Sequencer.Status.Done;
            }},

            new BodePlotStep("Channel 2", 1, 21, 0.8, false, Variables),

            new Step("Connect SDG2042X"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetSdgChannel(2);
                cancellationToken.WaitHandle.WaitOne(2000);
                return Sequencer.Status.Done;
            }},

            new BodePlotStep("Channel 3", 2, 21, 0.8, false, Variables),

            new Step("Connect SDG2042X"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetSdgChannel(3);
                cancellationToken.WaitHandle.WaitOne(2000);
                return Sequencer.Status.Done;
            }},

            new BodePlotStep("Channel 4", 3, 21, 0.8, false, Variables),

            new Step("Disconnect SDG2042X"){ Action = (CancellationToken cancellationToken) => { Instruments.Instance.SetSdgChannel(-1); return Sequencer.Status.Done; }},

            new Step("Cleanup"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.Close();
                return Sequencer.Status.Done;
            }},
        ];
    }
}
