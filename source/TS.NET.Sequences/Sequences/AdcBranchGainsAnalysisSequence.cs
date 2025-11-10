using TS.NET.Photino;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class AdcBranchGainsAnalysisSequence : Sequence
{
    public BenchCalibrationVariables Variables { get; private set; }

    public AdcBranchGainsAnalysisSequence(Func<Dialog, DialogResult> uiDialog, BenchCalibrationVariables variables)
    {
        Name = "ADC branch gains analysis";
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

            new AdcBranchGainsStep("ADC branch gains - Channel 1 - 1 GSPS", 0, 1_000_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 1 - 660 MSPS", 0, 660_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 1 - 500 MSPS", 0, 500_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 1 - 330 MSPS", 0, 330_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 1 - 250 MSPS", 0, 250_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 1 - 165 MSPS", 0, 165_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 1 - 100 MSPS", 0, 100_000_000, Variables),

            new AdcBranchGainsStep("ADC branch gains - Channel 2 - 1 GSPS", 1, 1_000_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 2 - 660 MSPS", 1, 660_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 2 - 500 MSPS", 1, 500_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 2 - 330 MSPS", 1, 330_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 2 - 250 MSPS", 1, 250_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 2 - 165 MSPS", 1, 165_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 2 - 100 MSPS", 1, 100_000_000, Variables),

            new AdcBranchGainsStep("ADC branch gains - Channel 3 - 1 GSPS", 2, 1_000_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 3 - 660 MSPS", 2, 660_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 3 - 500 MSPS", 2, 500_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 3 - 330 MSPS", 2, 330_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 3 - 250 MSPS", 2, 250_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 3 - 165 MSPS", 2, 165_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 3 - 100 MSPS", 2, 100_000_000, Variables),

            new AdcBranchGainsStep("ADC branch gains - Channel 4 - 1 GSPS", 3, 1_000_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 4 - 660 MSPS", 3, 660_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 4 - 500 MSPS", 3, 500_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 4 - 330 MSPS", 3, 330_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 4 - 250 MSPS", 3, 250_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 4 - 165 MSPS", 3, 165_000_000, Variables),
            new AdcBranchGainsStep("ADC branch gains - Channel 4 - 100 MSPS", 3, 100_000_000, Variables),

            new Step("Disconnect signal generator"){ Action = (CancellationToken cancellationToken) => { Instruments.Instance.SetSdgChannel(-1); return Sequencer.Status.Done; }},

            new Step("Cleanup"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.Close();
                return Sequencer.Status.Done;
            }},
        ];
    }
}
