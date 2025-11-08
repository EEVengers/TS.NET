using TS.NET.Photino;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class AdcFineGainAnalysisSequence : Sequence
{
    public BenchCalibrationVariables Variables { get; private set; }

    public AdcFineGainAnalysisSequence(Func<Dialog, DialogResult> uiDialog, BenchCalibrationVariables variables)
    {
        Name = "ADC fine gain analysis";
        Variables = variables;
        variables.WarmupTimeSec = 30;
        AddSteps(uiDialog);
        SetStepIndices();
    }

    private void AddSteps(Func<Dialog, DialogResult> uiDialog)
    {
        Steps =
        [
            new InitialiseDeviceStep("Initialise device", Variables),
            new InitialiseSigGensStep("Initialise instruments", Variables),
            new LoadUserCalFromDeviceFallbackToFileStep("Load calibration from device/file", Variables),
            new WarmupStep("Warmup device", Variables) { Skip = false, AllowSkip = true },

            new Step("Connect SDG2042X - Channel 1"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetSdgChannel(0);
                cancellationToken.WaitHandle.WaitOne(2000);
                return Sequencer.Status.Done;
            }},

            new AdcBranchGainsStep("ADC branch gains - Channel 1 - 1 GSPS", 0, 1_000_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 1 - 660 MSPS", 0, 660_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 1 - 500 MSPS", 0, 500_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 1 - 330 MSPS", 0, 330_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 1 - 250 MSPS", 0, 250_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 1 - 165 MSPS", 0, 165_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 1 - 100 MSPS", 0, 100_000_000, Variables),

            new Step("Connect SDG2042X - Channel 3"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetSdgChannel(2);
                cancellationToken.WaitHandle.WaitOne(2000);
                return Sequencer.Status.Done;
            }},

            new AdcBranchGainsStep("ADC branch gains - Channel 3 - 1 GSPS", 2, 1_000_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 3 - 660 MSPS", 2, 660_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 3 - 500 MSPS", 2, 500_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 3 - 330 MSPS", 2, 330_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 3 - 250 MSPS", 2, 250_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 3 - 165 MSPS", 2, 165_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 3 - 100 MSPS", 2, 100_000_000, Variables),

            new Step("Disconnect SDG2042X"){ Action = (CancellationToken cancellationToken) => { Instruments.Instance.SetSdgChannel(-1); return Sequencer.Status.Done; }},

            new Step("Cleanup"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.Close();
                return Sequencer.Status.Done;
            }},
        ];
    }
}
