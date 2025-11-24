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
            new InitialiseSigGensStep("Initialise signal generators", Variables),
            new LoadUserCalFromDeviceStep("Load calibration from device", Variables),
            new Step("Load ADC branch gains"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetThunderscopeAdcCalibration(Variables.Calibration.Adc.ToDriver());
                return Sequencer.Status.Done;
            }},
            new WarmupStep("Warmup device", Variables) { Skip = false, AllowSkip = true },

            new BodePlotStep("Channel 1 - Crossover flatness", channelIndex: 0, [0], 1_000_000_000, PgaPreampGain.Low, ladder: 10, attenuator: false, maxFrequency: 10_000_000, Variables),
            new BodePlotStep("Channel 2 - Crossover flatness", channelIndex: 1, [1], 1_000_000_000, PgaPreampGain.Low, ladder: 10, attenuator: false, maxFrequency: 10_000_000, Variables),
            new BodePlotStep("Channel 3 - Crossover flatness", channelIndex: 2, [2], 1_000_000_000, PgaPreampGain.Low, ladder: 10, attenuator: false, maxFrequency: 10_000_000, Variables),
            new BodePlotStep("Channel 4 - Crossover flatness", channelIndex: 3, [3], 1_000_000_000, PgaPreampGain.Low, ladder: 10, attenuator: false, maxFrequency: 10_000_000, Variables),

            // Note: SDG has 5 Vpp limit for frequencies above 10 MHz, use correct pregamp gain & ladder setting.
            new BodePlotStep("Channel 1 - Attenuator flatness", channelIndex: 0, [0], 1_000_000_000, PgaPreampGain.High, ladder: 10, attenuator: true, maxFrequency: 10_000_000, Variables),
            new BodePlotStep("Channel 2 - Attenuator flatness", channelIndex: 1, [1], 1_000_000_000, PgaPreampGain.High, ladder: 10, attenuator: true, maxFrequency: 10_000_000, Variables),
            new BodePlotStep("Channel 3 - Attenuator flatness", channelIndex: 2, [2], 1_000_000_000, PgaPreampGain.High, ladder: 10, attenuator: true, maxFrequency: 10_000_000, Variables),
            new BodePlotStep("Channel 4 - Attenuator flatness", channelIndex: 3, [3], 1_000_000_000, PgaPreampGain.High, ladder: 10, attenuator: true, maxFrequency: 10_000_000, Variables),

            new Step("Disconnect signal generator"){ Action = (CancellationToken cancellationToken) => { SigGens.Instance.SetSdgChannel([]); return Sequencer.Status.Done; }},

            new Step("Cleanup"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.Close();
                SigGens.Instance.Close();
                return Sequencer.Status.Done;
            }},
        ];
    }
}
