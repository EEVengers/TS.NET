using TS.NET.Photino;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class NoiseVerificationSequence : Sequence
{
    public NoiseVerificationVariables Variables { get; private set; }

    public NoiseVerificationSequence(Func<Dialog, DialogResult> uiDialog, NoiseVerificationVariables variables)
    {
        Name = "Noise verification";
        Variables = variables;
        AddSteps(uiDialog);
        SetStepIndices();
    }

    private void AddSteps(Func<Dialog, DialogResult> uiDialog)
    {
        Steps =
        [
            new DialogStep("Cable check", uiDialog){ Title = "Cable check", Text = "All cables disconnected from channels 1-4?", Buttons = DialogButtons.YesNo, Icon = DialogIcon.Question },
            new InitialiseDeviceStep("Initialise device", Variables),
            new LoadUserCalFromDeviceStep("Load calibration from device", Variables),
            //new WarmupStep("Warmup device", Variables) { Skip = false },

            new Step("Set channel 1"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetThunderscopeChannel([0]);
                Instruments.Instance.SetThunderscopeRate(1_000_000_000, Variables);
                return Sequencer.Status.Done;
            }},

            new AcRmsStep("Channel 1 - measure noise AC RMS - 50R, 1 GSPS, 20M, HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00006 },
            new AcRmsStep("Channel 1 - measure noise AC RMS - 50R, 500 MSPS, 20M, HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00006 },
            new AcRmsStep("Channel 1 - measure noise AC RMS - 50R, 250 MSPS, 20M, HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00006 },
            new AcRmsStep("Channel 1 - measure noise AC RMS - 50R, 100 MSPS, 20M, HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00006 },
            new AcRmsStep("Channel 1 - measure noise AC RMS - 1M, 1 GSPS, 20M, HG L0", 0, 0, ThunderscopeTermination.OneMegaohm, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00008 },
            new AcRmsStep("Channel 1 - measure noise AC RMS - 1M, 500 MSPS, 20M, HG L0", 0, 0, ThunderscopeTermination.OneMegaohm, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00008 },
            new AcRmsStep("Channel 1 - measure noise AC RMS - 1M, 250 MSPS, 20M, HG L0", 0, 0, ThunderscopeTermination.OneMegaohm, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00008 },
            new AcRmsStep("Channel 1 - measure noise AC RMS - 1M, 100 MSPS, 20M, HG L0", 0, 0, ThunderscopeTermination.OneMegaohm, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00008 },

            new Step("Set channel 2"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetThunderscopeChannel([1]);
                Instruments.Instance.SetThunderscopeRate(1_000_000_000, Variables);
                return Sequencer.Status.Done;
            }},

            new AcRmsStep("Channel 2 - measure noise AC RMS - 50R, 1 GSPS, 20M, HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00006 },
            new AcRmsStep("Channel 2 - measure noise AC RMS - 50R, 500 MSPS, 20M, HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00006 },
            new AcRmsStep("Channel 2 - measure noise AC RMS - 50R, 250 MSPS, 20M, HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00006 },
            new AcRmsStep("Channel 2 - measure noise AC RMS - 50R, 100 MSPS, 20M, HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00006 },
            new AcRmsStep("Channel 2 - measure noise AC RMS - 1M, 1 GSPS, 20M, HG L0", 1, 0, ThunderscopeTermination.OneMegaohm, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00008 },
            new AcRmsStep("Channel 2 - measure noise AC RMS - 1M, 500 MSPS, 20M, HG L0", 1, 0, ThunderscopeTermination.OneMegaohm, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00008 },
            new AcRmsStep("Channel 2 - measure noise AC RMS - 1M, 250 MSPS, 20M, HG L0", 1, 0, ThunderscopeTermination.OneMegaohm, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00008 },
            new AcRmsStep("Channel 2 - measure noise AC RMS - 1M, 100 MSPS, 20M, HG L0", 1, 0, ThunderscopeTermination.OneMegaohm, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00008 },

            new Step("Set channel 3"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetThunderscopeChannel([2]);
                Instruments.Instance.SetThunderscopeRate(1_000_000_000, Variables);
                return Sequencer.Status.Done;
            }},

            new AcRmsStep("Channel 3 - measure noise AC RMS - 50R, 1 GSPS, 20M, HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00006 },
            new AcRmsStep("Channel 3 - measure noise AC RMS - 50R, 500 MSPS, 20M, HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00006 },
            new AcRmsStep("Channel 3 - measure noise AC RMS - 50R, 250 MSPS, 20M, HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00006 },
            new AcRmsStep("Channel 3 - measure noise AC RMS - 50R, 100 MSPS, 20M, HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00006 },
            new AcRmsStep("Channel 3 - measure noise AC RMS - 1M, 1 GSPS, 20M, HG L0", 2, 0, ThunderscopeTermination.OneMegaohm, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00008 },
            new AcRmsStep("Channel 3 - measure noise AC RMS - 1M, 500 MSPS, 20M, HG L0", 2, 0, ThunderscopeTermination.OneMegaohm, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00008 },
            new AcRmsStep("Channel 3 - measure noise AC RMS - 1M, 250 MSPS, 20M, HG L0", 2, 0, ThunderscopeTermination.OneMegaohm, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00008 },
            new AcRmsStep("Channel 3 - measure noise AC RMS - 1M, 100 MSPS, 20M, HG L0", 2, 0, ThunderscopeTermination.OneMegaohm, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00008 },

            new Step("Set channel 4"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetThunderscopeChannel([3]);
                Instruments.Instance.SetThunderscopeRate(1_000_000_000, Variables);
                return Sequencer.Status.Done;
            }},

            new AcRmsStep("Channel 4 - measure noise AC RMS - 50R, 1 GSPS, 20M, HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00006 },
            new AcRmsStep("Channel 4 - measure noise AC RMS - 50R, 500 MSPS, 20M, HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00006 },
            new AcRmsStep("Channel 4 - measure noise AC RMS - 50R, 250 MSPS, 20M, HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00006 },
            new AcRmsStep("Channel 4 - measure noise AC RMS - 50R, 100 MSPS, 20M, HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00006 },
            new AcRmsStep("Channel 4 - measure noise AC RMS - 1M, 1 GSPS, 20M, HG L0", 3, 0, ThunderscopeTermination.OneMegaohm, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00008 },
            new AcRmsStep("Channel 4 - measure noise AC RMS - 1M, 500 MSPS, 20M, HG L0", 3, 0, ThunderscopeTermination.OneMegaohm, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00008 },
            new AcRmsStep("Channel 4 - measure noise AC RMS - 1M, 250 MSPS, 20M, HG L0", 3, 0, ThunderscopeTermination.OneMegaohm, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00008 },
            new AcRmsStep("Channel 4 - measure noise AC RMS - 1M, 100 MSPS, 20M, HG L0", 3, 0, ThunderscopeTermination.OneMegaohm, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00008 },

            new Step("Cleanup"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.Close();
                return Sequencer.Status.Done;
            }},
        ];
    }
}
