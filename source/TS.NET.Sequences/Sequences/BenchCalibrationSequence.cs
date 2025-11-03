using TS.NET.Photino;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class BenchCalibrationSequence : Sequence
{
    public BenchCalibrationVariables Variables { get; private set; }

    public BenchCalibrationSequence(Func<Dialog, DialogResult> uiDialog, BenchCalibrationVariables variables)
    {
        Name = "Bench calibration";
        Variables = variables;
        AddSteps(uiDialog);
        SetStepIndices();
    }

    private void AddSteps(Func<Dialog, DialogResult> uiDialog)
    {
        Steps =
        [
            new DialogStep("Cable check", uiDialog){ Title = "Cable check", Text = "Cables connected from 2x SDG2042X to channels 1-4?", Buttons = DialogButtons.YesNo, Icon = DialogIcon.Question },
            new InitialiseDeviceStep("Initialise device", Variables),
            new InitialiseSigGensStep("Initialise instruments", Variables),
            new LoadUserCalFromDeviceFallbackToFileStep("Load calibration from device/file", Variables),
            new WarmupStep("Warmup device", Variables) { Skip = false, AllowSkip = true },

            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - HG L0", 0, 0, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - HG L1", 0, 1, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - HG L2", 0, 2, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - HG L3", 0, 3, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - HG L4", 0, 4, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - HG L5", 0, 5, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - HG L6", 0, 6, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - HG L7", 0, 7, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - HG L8", 0, 8, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - HG L9", 0, 9, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - HG L10", 0, 10, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - LG L0", 0, 11, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - LG L1", 0, 12, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - LG L2", 0, 13, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - LG L3", 0, 14, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - LG L4", 0, 15, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - LG L5", 0, 16, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - LG L6", 0, 17, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - LG L7", 0, 18, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - LG L8", 0, 19, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - LG L9", 0, 20, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC scale - LG L10", 0, 21, Variables),

            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L0", 0, 0, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L1", 0, 1, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L2", 0, 2, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L3", 0, 3, Variables),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L4", 0, 4, Variables),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L5", 0, 5, Variables),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L6", 0, 6, Variables),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L7", 0, 7, Variables),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L8", 0, 8, Variables),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L9", 0, 9, Variables),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L10", 0, 10, Variables),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L0", 0, 11, Variables),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L1", 0, 12, Variables),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L2", 0, 13, Variables),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L3", 0, 14, Variables),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L4", 0, 15, Variables),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L5", 0, 16, Variables),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L6", 0, 17, Variables),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L7", 0, 18, Variables),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L8", 0, 19, Variables),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L9", 0, 20, Variables),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L10", 0, 21, Variables),

            new Step("Connect SDG2042X"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetSdgChannel(0);
                cancellationToken.WaitHandle.WaitOne(2000);
                return Sequencer.Status.Done;
            }},
            new Step("Find SDG2042X zero"){ Action = (CancellationToken cancellationToken) => {
                var pathConfig = Utility.GetChannelPathConfig(0, 0, Variables);
                var pathCalibration = Utility.GetChannelPathCalibration(0, 0, Variables);
                Instruments.Instance.SetThunderscopeCalManual1M(0, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, Variables);
                Utility.GetAndCheckSigGenZero(0, pathConfig, Variables, cancellationToken);
                return Sequencer.Status.Done;
            }},

            new AdcFineGainStep("Measure & set ADC fine gain", Variables),

            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L0", 0, 0, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L1", 0, 1, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L2", 0, 2, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L3", 0, 3, Variables),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L4", 0, 4, Variables),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L5", 0, 5, Variables),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L6", 0, 6, Variables),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L7", 0, 7, Variables),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L8", 0, 8, Variables),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L9", 0, 9, Variables),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L10", 0, 10, Variables),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L0", 0, 11, Variables),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L1", 0, 12, Variables),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L2", 0, 13, Variables),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L3", 0, 14, Variables),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L4", 0, 15, Variables),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L5", 0, 16, Variables),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L6", 0, 17, Variables),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L7", 0, 18, Variables),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L8", 0, 19, Variables),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L9", 0, 20, Variables),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L10", 0, 21, Variables),
            new AttenuatorStep("Channel 1 - measure attenuator scale", 0, Variables),

            new PgaLoadSetupStep("Channel 1 - setup PGA loading scale", 0, Variables),
            new PgaLoadStep("Channel 1 - measure PGA loading scale - 660 MSPS, 1 channel", 0, [0], 660_000_000, Variables),
            new PgaLoadStep("Channel 1 - measure PGA loading scale - 500 MSPS, 1 channel", 0, [0], 500_000_000, Variables),
            new PgaLoadStep("Channel 1 - measure PGA loading scale - 330 MSPS, 1 channel", 0, [0], 330_000_000, Variables),
            new PgaLoadStep("Channel 1 - measure PGA loading scale - 250 MSPS, 1 channel", 0, [0], 250_000_000, Variables),
            new PgaLoadStep("Channel 1 - measure PGA loading scale - 165 MSPS, 1 channel", 0, [0], 165_000_000, Variables),
            new PgaLoadStep("Channel 1 - measure PGA loading scale - 100 MSPS, 1 channel", 0, [0], 100_000_000, Variables),
            new PgaLoadStep("Channel 1 - measure PGA loading scale - 500 MSPS, 2 channel", 0, [0, 1], 500_000_000, Variables),
            new PgaLoadStep("Channel 1 - measure PGA loading scale - 330 MSPS, 2 channel", 0, [0, 1], 330_000_000, Variables),
            new PgaLoadStep("Channel 1 - measure PGA loading scale - 250 MSPS, 2 channel", 0, [0, 1], 250_000_000, Variables),
            new PgaLoadStep("Channel 1 - measure PGA loading scale - 165 MSPS, 2 channel", 0, [0, 1], 165_000_000, Variables),
            new PgaLoadStep("Channel 1 - measure PGA loading scale - 100 MSPS, 2 channel", 0, [0, 1], 100_000_000, Variables),
            new PgaLoadStep("Channel 1 - measure PGA loading scale - 250 MSPS, 3 channel", 0, [0, 1, 2], 250_000_000, Variables),
            new PgaLoadStep("Channel 1 - measure PGA loading scale - 165 MSPS, 3 channel", 0, [0, 1, 2], 165_000_000, Variables),
            new PgaLoadStep("Channel 1 - measure PGA loading scale - 100 MSPS, 3 channel", 0, [0, 1, 2], 100_000_000, Variables),
            new PgaLoadStep("Channel 1 - measure PGA loading scale - 250 MSPS, 4 channel", 0, [0, 1, 2, 3], 250_000_000, Variables),
            new PgaLoadStep("Channel 1 - measure PGA loading scale - 165 MSPS, 4 channel", 0, [0, 1, 2, 3], 165_000_000, Variables),
            new PgaLoadStep("Channel 1 - measure PGA loading scale - 100 MSPS, 4 channel", 0, [0, 1, 2, 3], 100_000_000, Variables),

            new Step("Disconnect SDG2042X"){ Action = (CancellationToken cancellationToken) => { Instruments.Instance.SetSdgChannel(-1); return Sequencer.Status.Done; }},

            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - HG L0", 1, 0, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - HG L1", 1, 1, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - HG L2", 1, 2, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - HG L3", 1, 3, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - HG L4", 1, 4, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - HG L5", 1, 5, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - HG L6", 1, 6, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - HG L7", 1, 7, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - HG L8", 1, 8, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - HG L9", 1, 9, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - HG L10", 1, 10, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - LG L0", 1, 11, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - LG L1", 1, 12, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - LG L2", 1, 13, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - LG L3", 1, 14, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - LG L4", 1, 15, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - LG L5", 1, 16, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - LG L6", 1, 17, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - LG L7", 1, 18, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - LG L8", 1, 19, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - LG L9", 1, 20, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC scale - LG L10", 1, 21, Variables),

            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L0", 1, 0, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L1", 1, 1, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L2", 1, 2, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L3", 1, 3, Variables),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L4", 1, 4, Variables),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L5", 1, 5, Variables),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L6", 1, 6, Variables),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L7", 1, 7, Variables),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L8", 1, 8, Variables),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L9", 1, 9, Variables),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L10", 1, 10, Variables),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L0", 1, 11, Variables),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L1", 1, 12, Variables),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L2", 1, 13, Variables),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L3", 1, 14, Variables),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L4", 1, 15, Variables),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L5", 1, 16, Variables),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L6", 1, 17, Variables),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L7", 1, 18, Variables),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L8", 1, 19, Variables),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L9", 1, 20, Variables),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L10", 1, 21, Variables),

            new Step("Connect SDG2042X"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetSdgChannel(1);
                cancellationToken.WaitHandle.WaitOne(2000);
                return Sequencer.Status.Done;
            }},
            new Step("Find SDG2042X zero"){ Action = (CancellationToken cancellationToken) => {
                var pathConfig = Utility.GetChannelPathConfig(1, 0, Variables);
                var pathCalibration = Utility.GetChannelPathCalibration(1, 0, Variables);
                Instruments.Instance.SetThunderscopeCalManual1M(1, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, Variables);
                Utility.GetAndCheckSigGenZero(1, pathConfig, Variables, cancellationToken);
                return Sequencer.Status.Done;
            }},

            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L0", 1, 0, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L1", 1, 1, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L2", 1, 2, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L3", 1, 3, Variables),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L4", 1, 4, Variables),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L5", 1, 5, Variables),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L6", 1, 6, Variables),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L7", 1, 7, Variables),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L8", 1, 8, Variables),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L9", 1, 9, Variables),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L10", 1, 10, Variables),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L0", 1, 11, Variables),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L1", 1, 12, Variables),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L2", 1, 13, Variables),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L3", 1, 14, Variables),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L4", 1, 15, Variables),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L5", 1, 16, Variables),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L6", 1, 17, Variables),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L7", 1, 18, Variables),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L8", 1, 19, Variables),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L9", 1, 20, Variables),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L10", 1, 21, Variables),
            new AttenuatorStep("Channel 2 - measure attenuator scale", 1, Variables),

            new PgaLoadSetupStep("Channel 2 - setup PGA loading scale", 1, Variables),
            new PgaLoadStep("Channel 2 - measure PGA loading scale - 660 MSPS, 1 channel", 1, [1], 660_000_000, Variables),
            new PgaLoadStep("Channel 2 - measure PGA loading scale - 500 MSPS, 1 channel", 1, [1], 500_000_000, Variables),
            new PgaLoadStep("Channel 2 - measure PGA loading scale - 330 MSPS, 1 channel", 1, [1], 330_000_000, Variables),
            new PgaLoadStep("Channel 2 - measure PGA loading scale - 250 MSPS, 1 channel", 1, [1], 250_000_000, Variables),
            new PgaLoadStep("Channel 2 - measure PGA loading scale - 165 MSPS, 1 channel", 1, [1], 165_000_000, Variables),
            new PgaLoadStep("Channel 2 - measure PGA loading scale - 100 MSPS, 1 channel", 1, [1], 100_000_000, Variables),
            new PgaLoadStep("Channel 2 - measure PGA loading scale - 500 MSPS, 2 channel", 1, [1, 2], 500_000_000, Variables),
            new PgaLoadStep("Channel 2 - measure PGA loading scale - 330 MSPS, 2 channel", 1, [1, 2], 330_000_000, Variables),
            new PgaLoadStep("Channel 2 - measure PGA loading scale - 250 MSPS, 2 channel", 1, [1, 2], 250_000_000, Variables),
            new PgaLoadStep("Channel 2 - measure PGA loading scale - 165 MSPS, 2 channel", 1, [1, 2], 165_000_000, Variables),
            new PgaLoadStep("Channel 2 - measure PGA loading scale - 100 MSPS, 2 channel", 1, [1, 2], 100_000_000, Variables),
            new PgaLoadStep("Channel 2 - measure PGA loading scale - 250 MSPS, 3 channel", 1, [1, 2, 3], 250_000_000, Variables),
            new PgaLoadStep("Channel 2 - measure PGA loading scale - 165 MSPS, 3 channel", 1, [1, 2, 3], 165_000_000, Variables),
            new PgaLoadStep("Channel 2 - measure PGA loading scale - 100 MSPS, 3 channel", 1, [1, 2, 3], 100_000_000, Variables),
            new PgaLoadStep("Channel 2 - measure PGA loading scale - 250 MSPS, 4 channel", 1, [0, 1, 2, 3], 250_000_000, Variables),
            new PgaLoadStep("Channel 2 - measure PGA loading scale - 165 MSPS, 4 channel", 1, [0, 1, 2, 3], 165_000_000, Variables),
            new PgaLoadStep("Channel 2 - measure PGA loading scale - 100 MSPS, 4 channel", 1, [0, 1, 2, 3], 100_000_000, Variables),

            new Step("Disconnect SDG2042X"){ Action = (CancellationToken cancellationToken) => { Instruments.Instance.SetSdgChannel(-1); return Sequencer.Status.Done; }},

            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - HG L0", 2, 0, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - HG L1", 2, 1, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - HG L2", 2, 2, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - HG L3", 2, 3, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - HG L4", 2, 4, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - HG L5", 2, 5, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - HG L6", 2, 6, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - HG L7", 2, 7, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - HG L8", 2, 8, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - HG L9", 2, 9, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - HG L10", 2, 10, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - LG L0", 2, 11, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - LG L1", 2, 12, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - LG L2", 2, 13, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - LG L3", 2, 14, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - LG L4", 2, 15, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - LG L5", 2, 16, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - LG L6", 2, 17, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - LG L7", 2, 18, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - LG L8", 2, 19, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - LG L9", 2, 20, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC scale - LG L10", 2, 21, Variables),

            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L0", 2, 0, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L1", 2, 1, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L2", 2, 2, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L3", 2, 3, Variables),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L4", 2, 4, Variables),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L5", 2, 5, Variables),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L6", 2, 6, Variables),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L7", 2, 7, Variables),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L8", 2, 8, Variables),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L9", 2, 9, Variables),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L10", 2, 10, Variables),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L0", 2, 11, Variables),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L1", 2, 12, Variables),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L2", 2, 13, Variables),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L3", 2, 14, Variables),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L4", 2, 15, Variables),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L5", 2, 16, Variables),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L6", 2, 17, Variables),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L7", 2, 18, Variables),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L8", 2, 19, Variables),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L9", 2, 20, Variables),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L10", 2, 21, Variables),

            new Step("Connect SDG2042X"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetSdgChannel(2);
                cancellationToken.WaitHandle.WaitOne(2000);
                return Sequencer.Status.Done;
            }},
            new Step("Find SDG2042X zero"){ Action = (CancellationToken cancellationToken) => {
                var pathConfig = Utility.GetChannelPathConfig(2, 0, Variables);
                var pathCalibration = Utility.GetChannelPathCalibration(2, 0, Variables);
                Instruments.Instance.SetThunderscopeCalManual1M(2, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, Variables);
                Utility.GetAndCheckSigGenZero(2, pathConfig, Variables, cancellationToken);
                return Sequencer.Status.Done;
            }},

            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L0", 2, 0, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L1", 2, 1, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L2", 2, 2, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L3", 2, 3, Variables),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L4", 2, 4, Variables),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L5", 2, 5, Variables),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L6", 2, 6, Variables),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L7", 2, 7, Variables),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L8", 2, 8, Variables),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L9", 2, 9, Variables),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L10", 2, 10, Variables),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L0", 2, 11, Variables),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L1", 2, 12, Variables),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L2", 2, 13, Variables),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L3", 2, 14, Variables),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L4", 2, 15, Variables),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L5", 2, 16, Variables),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L6", 2, 17, Variables),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L7", 2, 18, Variables),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L8", 2, 19, Variables),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L9", 2, 20, Variables),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L10", 2, 21, Variables),
            new AttenuatorStep("Channel 3 - measure attenuator scale", 2, Variables),

            new PgaLoadSetupStep("Channel 3 - setup PGA loading scale", 2, Variables),
            new PgaLoadStep("Channel 3 - measure PGA loading scale - 660 MSPS, 1 channel", 2, [2], 660_000_000, Variables),
            new PgaLoadStep("Channel 3 - measure PGA loading scale - 500 MSPS, 1 channel", 2, [2], 500_000_000, Variables),
            new PgaLoadStep("Channel 3 - measure PGA loading scale - 330 MSPS, 1 channel", 2, [2], 330_000_000, Variables),
            new PgaLoadStep("Channel 3 - measure PGA loading scale - 250 MSPS, 1 channel", 2, [2], 250_000_000, Variables),
            new PgaLoadStep("Channel 3 - measure PGA loading scale - 165 MSPS, 1 channel", 2, [2], 165_000_000, Variables),
            new PgaLoadStep("Channel 3 - measure PGA loading scale - 100 MSPS, 1 channel", 2, [2], 100_000_000, Variables),
            new PgaLoadStep("Channel 3 - measure PGA loading scale - 500 MSPS, 2 channel", 2, [2, 3], 500_000_000, Variables),
            new PgaLoadStep("Channel 3 - measure PGA loading scale - 330 MSPS, 2 channel", 2, [2, 3], 330_000_000, Variables),
            new PgaLoadStep("Channel 3 - measure PGA loading scale - 250 MSPS, 2 channel", 2, [2, 3], 250_000_000, Variables),
            new PgaLoadStep("Channel 3 - measure PGA loading scale - 165 MSPS, 2 channel", 2, [2, 3], 165_000_000, Variables),
            new PgaLoadStep("Channel 3 - measure PGA loading scale - 100 MSPS, 2 channel", 2, [2, 3], 100_000_000, Variables),
            new PgaLoadStep("Channel 3 - measure PGA loading scale - 250 MSPS, 3 channel", 2, [2, 3, 0], 250_000_000, Variables),
            new PgaLoadStep("Channel 3 - measure PGA loading scale - 165 MSPS, 3 channel", 2, [2, 3, 0], 165_000_000, Variables),
            new PgaLoadStep("Channel 3 - measure PGA loading scale - 100 MSPS, 3 channel", 2, [2, 3, 0], 100_000_000, Variables),
            new PgaLoadStep("Channel 3 - measure PGA loading scale - 250 MSPS, 4 channel", 2, [0, 1, 2, 3], 250_000_000, Variables),
            new PgaLoadStep("Channel 3 - measure PGA loading scale - 165 MSPS, 4 channel", 2, [0, 1, 2, 3], 165_000_000, Variables),
            new PgaLoadStep("Channel 3 - measure PGA loading scale - 100 MSPS, 4 channel", 2, [0, 1, 2, 3], 100_000_000, Variables),

            new Step("Disconnect SDG2042X"){ Action = (CancellationToken cancellationToken) => { Instruments.Instance.SetSdgChannel(-1); return Sequencer.Status.Done; }},

            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - HG L0", 3, 0, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - HG L1", 3, 1, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - HG L2", 3, 2, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - HG L3", 3, 3, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - HG L4", 3, 4, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - HG L5", 3, 5, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - HG L6", 3, 6, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - HG L7", 3, 7, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - HG L8", 3, 8, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - HG L9", 3, 9, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - HG L10", 3, 10, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - LG L0", 3, 11, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - LG L1", 3, 12, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - LG L2", 3, 13, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - LG L3", 3, 14, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - LG L4", 3, 15, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - LG L5", 3, 16, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - LG L6", 3, 17, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - LG L7", 3, 18, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - LG L8", 3, 19, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - LG L9", 3, 20, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC scale - LG L10", 3, 21, Variables),

            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L0", 3, 0, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L1", 3, 1, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L2", 3, 2, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L3", 3, 3, Variables),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L4", 3, 4, Variables),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L5", 3, 5, Variables),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L6", 3, 6, Variables),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L7", 3, 7, Variables),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L8", 3, 8, Variables),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L9", 3, 9, Variables),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L10", 3, 10, Variables),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L0", 3, 11, Variables),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L1", 3, 12, Variables),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L2", 3, 13, Variables),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L3", 3, 14, Variables),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L4", 3, 15, Variables),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L5", 3, 16, Variables),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L6", 3, 17, Variables),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L7", 3, 18, Variables),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L8", 3, 19, Variables),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L9", 3, 20, Variables),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L10", 3, 21, Variables),

            new Step("Connect SDG2042X"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetSdgChannel(3);
                cancellationToken.WaitHandle.WaitOne(2000);
                return Sequencer.Status.Done;
            }},
            new Step("Find SDG2042X zero"){ Action = (CancellationToken cancellationToken) => {
                var pathConfig = Utility.GetChannelPathConfig(3, 0, Variables);
                var pathCalibration = Utility.GetChannelPathCalibration(3, 0, Variables);
                Instruments.Instance.SetThunderscopeCalManual1M(3, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, Variables);
                Utility.GetAndCheckSigGenZero(3, pathConfig, Variables, cancellationToken);
                return Sequencer.Status.Done;
            }},

            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L0", 3, 0, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L1", 3, 1, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L2", 3, 2, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L3", 3, 3, Variables),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L4", 3, 4, Variables),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L5", 3, 5, Variables),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L6", 3, 6, Variables),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L7", 3, 7, Variables),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L8", 3, 8, Variables),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L9", 3, 9, Variables),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L10", 3, 10, Variables),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L0", 3, 11, Variables),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L1", 3, 12, Variables),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L2", 3, 13, Variables),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L3", 3, 14, Variables),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L4", 3, 15, Variables),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L5", 3, 16, Variables),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L6", 3, 17, Variables),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L7", 3, 18, Variables),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L8", 3, 19, Variables),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L9", 3, 20, Variables),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L10", 3, 21, Variables),
            new AttenuatorStep("Channel 4 - measure attenuator scale", 3, Variables),

            new PgaLoadSetupStep("Channel 4 - setup PGA loading scale", 3, Variables),
            new PgaLoadStep("Channel 4 - measure PGA loading scale - 660 MSPS, 1 channel", 3, [3], 660_000_000, Variables),
            new PgaLoadStep("Channel 4 - measure PGA loading scale - 500 MSPS, 1 channel", 3, [3], 500_000_000, Variables),
            new PgaLoadStep("Channel 4 - measure PGA loading scale - 330 MSPS, 1 channel", 3, [3], 330_000_000, Variables),
            new PgaLoadStep("Channel 4 - measure PGA loading scale - 250 MSPS, 1 channel", 3, [3], 250_000_000, Variables),
            new PgaLoadStep("Channel 4 - measure PGA loading scale - 165 MSPS, 1 channel", 3, [3], 165_000_000, Variables),
            new PgaLoadStep("Channel 4 - measure PGA loading scale - 100 MSPS, 1 channel", 3, [3], 100_000_000, Variables),
            new PgaLoadStep("Channel 4 - measure PGA loading scale - 500 MSPS, 2 channel", 3, [3, 0], 500_000_000, Variables),
            new PgaLoadStep("Channel 4 - measure PGA loading scale - 330 MSPS, 2 channel", 3, [3, 0], 330_000_000, Variables),
            new PgaLoadStep("Channel 4 - measure PGA loading scale - 250 MSPS, 2 channel", 3, [3, 0], 250_000_000, Variables),
            new PgaLoadStep("Channel 4 - measure PGA loading scale - 165 MSPS, 2 channel", 3, [3, 0], 165_000_000, Variables),
            new PgaLoadStep("Channel 4 - measure PGA loading scale - 100 MSPS, 2 channel", 3, [3, 0], 100_000_000, Variables),
            new PgaLoadStep("Channel 4 - measure PGA loading scale - 250 MSPS, 3 channel", 3, [3, 0, 1], 250_000_000, Variables),
            new PgaLoadStep("Channel 4 - measure PGA loading scale - 165 MSPS, 3 channel", 3, [3, 0, 1], 165_000_000, Variables),
            new PgaLoadStep("Channel 4 - measure PGA loading scale - 100 MSPS, 3 channel", 3, [3, 0, 1], 100_000_000, Variables),
            new PgaLoadStep("Channel 4 - measure PGA loading scale - 250 MSPS, 4 channel", 3, [0, 1, 2, 3], 250_000_000, Variables),
            new PgaLoadStep("Channel 4 - measure PGA loading scale - 165 MSPS, 4 channel", 3, [0, 1, 2, 3], 165_000_000, Variables),
            new PgaLoadStep("Channel 4 - measure PGA loading scale - 100 MSPS, 4 channel", 3, [0, 1, 2, 3], 100_000_000, Variables),

            new Step("Disconnect SDG2042X"){ Action = (CancellationToken cancellationToken) => { Instruments.Instance.SetSdgChannel(-1); return Sequencer.Status.Done; }},

            //new SaveUserCalToFileStep("Save calibration to file", Variables),
            new SaveUserCalToDeviceStep("Save calibration to device", Variables),

            new Step("Cleanup"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.Close();
                return Sequencer.Status.Done;
            }},
        ];
    }
}
