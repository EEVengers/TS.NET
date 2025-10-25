﻿using TS.NET.Photino;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class SelfCalibrationSequence : Sequence
{
    public SelfCalibrationVariables Variables { get; private set; }

    public SelfCalibrationSequence(Func<Dialog, DialogResult> uiDialog, SelfCalibrationVariables variables)
    {
        Name = "Self calibration";
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
            new LoadUserCalFromDeviceFallbackToFileStep("Load calibration from device/file", Variables),
            new WarmupStep("Warmup device", Variables) { Skip = false },

            new Step("Set channel 1"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetThunderscopeChannel([0]);
                return Sequencer.Status.Done;
            }},

            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L0", 0, 0, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L1", 0, 1, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L2", 0, 2, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L3", 0, 3, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L4", 0, 4, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L5", 0, 5, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L6", 0, 6, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L7", 0, 7, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L8", 0, 8, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L9", 0, 9, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L10", 0, 10, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L0", 0, 11, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L1", 0, 12, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L2", 0, 13, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L3", 0, 14, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L4", 0, 15, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L5", 0, 16, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L6", 0, 17, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L7", 0, 18, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L8", 0, 19, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L9", 0, 20, Variables),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L10", 0, 21, Variables),

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

            new Step("Set channel 2"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetThunderscopeChannel([1]);
                return Sequencer.Status.Done;
            }},

            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L0", 1, 0, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L1", 1, 1, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L2", 1, 2, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L3", 1, 3, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L4", 1, 4, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L5", 1, 5, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L6", 1, 6, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L7", 1, 7, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L8", 1, 8, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L9", 1, 9, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L10", 1, 10, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L0", 1, 11, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L1", 1, 12, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L2", 1, 13, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L3", 1, 14, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L4", 1, 15, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L5", 1, 16, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L6", 1, 17, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L7", 1, 18, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L8", 1, 19, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L9", 1, 20, Variables),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L10", 1, 21, Variables),

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

            new Step("Set Channel 3"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetThunderscopeChannel([2]);
                return Sequencer.Status.Done;
            }},

            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L0", 2, 0, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L1", 2, 1, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L2", 2, 2, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L3", 2, 3, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L4", 2, 4, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L5", 2, 5, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L6", 2, 6, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L7", 2, 7, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L8", 2, 8, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L9", 2, 9, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L10", 2, 10, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L0", 2, 11, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L1", 2, 12, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L2", 2, 13, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L3", 2, 14, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L4", 2, 15, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L5", 2, 16, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L6", 2, 17, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L7", 2, 18, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L8", 2, 19, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L9", 2, 20, Variables),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L10", 2, 21, Variables),

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

            new Step("Set Channel 4"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetThunderscopeChannel([3]);
                return Sequencer.Status.Done;
            }},

            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L0", 3, 0, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L1", 3, 1, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L2", 3, 2, Variables) { IgnoreError = true, Timeout = TimeSpan.FromSeconds(30), Retries = 3 },
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L3", 3, 3, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L4", 3, 4, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L5", 3, 5, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L6", 3, 6, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L7", 3, 7, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L8", 3, 8, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L9", 3, 9, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L10", 3, 10, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L0", 3, 11, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L1", 3, 12, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L2", 3, 13, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L3", 3, 14, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L4", 3, 15, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L5", 3, 16, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L6", 3, 17, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L7", 3, 18, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L8", 3, 19, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L9", 3, 20, Variables),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L10", 3, 21, Variables),

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

            new SaveUserCalToFileStep("Save calibration to file", Variables),
            new SaveUserCalToDeviceStep("Save calibration to device", Variables),

            new Step("Cleanup"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.Close();
                return Sequencer.Status.Done;
            }},
        ];
    }
}
