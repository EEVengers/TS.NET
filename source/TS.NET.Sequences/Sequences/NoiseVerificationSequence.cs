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
            new Step("Load ADC branch gains"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetThunderscopeAdcCalibration(Variables.Calibration.Adc.ToDriver());
                return Sequencer.Status.Done;
            }},
            new WarmupStep("Warmup device", Variables) { Skip = false, AllowSkip = true },

            new AcRmsStep("Channel 1 - AC RMS - 50R, 8-bit, 1 GSPS, BW 20M, PGA HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            // new AcRmsStep("Channel 1 - AC RMS - 50R, 500 MSPS, 20M, HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 1 - AC RMS - 50R, 250 MSPS, 20M, HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 1 - AC RMS - 50R, 100 MSPS, 20M, HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            new AcRmsStep("Channel 1 - AC RMS - 50R, 8-bit, 1 GSPS, BW 100M, PGA HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            // new AcRmsStep("Channel 1 - AC RMS - 50R, 500 MSPS, 100M, HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 1 - AC RMS - 50R, 250 MSPS, 100M, HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 1 - AC RMS - 50R, 100 MSPS, 100M, HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            new AcRmsStep("Channel 1 - AC RMS - 50R, 8-bit, 1 GSPS, BW 350M, PGA HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            // new AcRmsStep("Channel 1 - AC RMS - 50R, 500 MSPS, 350M, HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 1 - AC RMS - 50R, 250 MSPS, 350M, HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 1 - AC RMS - 50R, 100 MSPS, 350M, HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            new AcRmsStep("Channel 1 - AC RMS - 50R, 8-bit, 1 GSPS, BW FULL, PGA HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            // new AcRmsStep("Channel 1 - AC RMS - 50R, 500 MSPS, FULL, HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 1 - AC RMS - 50R, 250 MSPS, FULL, HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 1 - AC RMS - 50R, 100 MSPS, FUL, HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },

            new AcRmsStep("Channel 2 - AC RMS - 50R, 8-bit, 1 GSPS, BW 20M, PGA HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            // new AcRmsStep("Channel 2 - AC RMS - 50R, 500 MSPS, 20M, HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 2 - AC RMS - 50R, 250 MSPS, 20M, HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 2 - AC RMS - 50R, 100 MSPS, 20M, HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            new AcRmsStep("Channel 2 - AC RMS - 50R, 8-bit, 1 GSPS, BW 100M, PGA HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            // new AcRmsStep("Channel 2 - AC RMS - 50R, 500 MSPS, 100M, HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 2 - AC RMS - 50R, 250 MSPS, 100M, HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 2 - AC RMS - 50R, 100 MSPS, 100M, HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            new AcRmsStep("Channel 2 - AC RMS - 50R, 8-bit, 1 GSPS, BW 350M, PGA HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            // new AcRmsStep("Channel 2 - AC RMS - 50R, 500 MSPS, 350M, HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 2 - AC RMS - 50R, 250 MSPS, 350M, HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 2 - AC RMS - 50R, 100 MSPS, 350M, HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            new AcRmsStep("Channel 2 - AC RMS - 50R, 8-bit, 1 GSPS, BW FULL, PGA HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            // new AcRmsStep("Channel 2 - AC RMS - 50R, 500 MSPS, FULL, HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 2 - AC RMS - 50R, 250 MSPS, FULL, HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 2 - AC RMS - 50R, 100 MSPS, FUL, HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },

            new AcRmsStep("Channel 3 - AC RMS - 50R, 8-bit, 1 GSPS, BW 20M, PGA HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            // new AcRmsStep("Channel 3 - AC RMS - 50R, 500 MSPS, 20M, HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 3 - AC RMS - 50R, 250 MSPS, 20M, HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 3 - AC RMS - 50R, 100 MSPS, 20M, HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            new AcRmsStep("Channel 3 - AC RMS - 50R, 8-bit, 1 GSPS, BW 100M, PGA HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            // new AcRmsStep("Channel 3 - AC RMS - 50R, 500 MSPS, 100M, HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 3 - AC RMS - 50R, 250 MSPS, 100M, HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 3 - AC RMS - 50R, 100 MSPS, 100M, HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            new AcRmsStep("Channel 3 - AC RMS - 50R, 8-bit, 1 GSPS, BW 350M, PGA HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            // new AcRmsStep("Channel 3 - AC RMS - 50R, 500 MSPS, 350M, HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 3 - AC RMS - 50R, 250 MSPS, 350M, HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 3 - AC RMS - 50R, 100 MSPS, 350M, HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            new AcRmsStep("Channel 3 - AC RMS - 50R, 8-bit, 1 GSPS, BW FULL, PGA HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            // new AcRmsStep("Channel 3 - AC RMS - 50R, 500 MSPS, FULL, HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 3 - AC RMS - 50R, 250 MSPS, FULL, HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 3 - AC RMS - 50R, 100 MSPS, FUL, HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },

            new AcRmsStep("Channel 4 - AC RMS - 50R, 8-bit, 1 GSPS, BW 20M, PGA HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            // new AcRmsStep("Channel 4 - AC RMS - 50R, 500 MSPS, 20M, HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 4 - AC RMS - 50R, 250 MSPS, 20M, HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 4 - AC RMS - 50R, 100 MSPS, 20M, HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            new AcRmsStep("Channel 4 - AC RMS - 50R, 8-bit, 1 GSPS, BW 100M, PGA HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            // new AcRmsStep("Channel 4 - AC RMS - 50R, 500 MSPS, 100M, HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 4 - AC RMS - 50R, 250 MSPS, 100M, HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 4 - AC RMS - 50R, 100 MSPS, 100M, HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            new AcRmsStep("Channel 4 - AC RMS - 50R, 8-bit, 1 GSPS, BW 350M, PGA HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            // new AcRmsStep("Channel 4 - AC RMS - 50R, 500 MSPS, 350M, HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 4 - AC RMS - 50R, 250 MSPS, 350M, HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 4 - AC RMS - 50R, 100 MSPS, 350M, HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            new AcRmsStep("Channel 4 - AC RMS - 50R, 8-bit, 1 GSPS, BW FULL, PGA HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            // new AcRmsStep("Channel 4 - AC RMS - 50R, 500 MSPS, FULL, HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 500_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 4 - AC RMS - 50R, 250 MSPS, FULL, HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 250_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },
            // new AcRmsStep("Channel 4 - AC RMS - 50R, 100 MSPS, FUL, HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 100_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009 },

            new Step("Cleanup"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.Close();
                return Sequencer.Status.Done;
            }},
        ];
    }
}
