using TS.NET.Sequencer;

namespace TS.NET.Calibration;

public class BenchCalibrationSequence : Sequence
{
    public BenchCalibrationSequence(Func<Dialog, DialogResult> uiDialog)
    {
        Name = "Bench calibration";
        Steps =
        [
            new DialogStep("Cable check", uiDialog){ Title = "Cable check", Text = "Cables connected from 2x SDG2042X to channels 1-4?", Buttons = DialogButtons.YesNo, Icon = DialogIcon.Question },
            new CalibrationFileExistsStep("Check calibration file exists"),
            new CalibrationFileLoadStep("Load calibration file"),
            new InitialiseInstrumentsStep("Initialise instruments", initSigGens: true),
            new WarmupStep("Warmup for 20 minutes") { Skip = true },

            new Step("Set channel 1"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetThunderscopeChannel([0]);
                return Sequencer.Status.Done;
            }},

            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L0", 0, 0),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L1", 0, 1),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L2", 0, 2),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L3", 0, 3),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L4", 0, 4),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L5", 0, 5),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L6", 0, 6),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L7", 0, 7),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L8", 0, 8),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L9", 0, 9),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - HG L10", 0, 10),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L0", 0, 11),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L1", 0, 12),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L2", 0, 13),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L3", 0, 14),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L4", 0, 15),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L5", 0, 16),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L6", 0, 17),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L7", 0, 18),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L8", 0, 19),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L9", 0, 20),
            new TrimOffsetDacGainStep("Channel 1 - measure trim offset DAC gain - LG L10", 0, 21),

            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L0", 0, 0),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L1", 0, 1),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L2", 0, 2),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L3", 0, 3),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L4", 0, 4),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L5", 0, 5),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L6", 0, 6),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L7", 0, 7),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L8", 0, 8),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L9", 0, 9),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - HG L10", 0, 10),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L0", 0, 11),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L1", 0, 12),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L2", 0, 13),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L3", 0, 14),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L4", 0, 15),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L5", 0, 16),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L6", 0, 17),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L7", 0, 18),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L8", 0, 19),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L9", 0, 20),
            new TrimOffsetDacZeroStep("Channel 1 - find trim offset DAC zero - LG L10", 0, 21),

            new Step("Connect SDG2042X"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.EnableSdgDc(0);
                cancellationToken.WaitHandle.WaitOne(2000);
                return Sequencer.Status.Done;
            }},
            new Step("Find SDG2042X zero"){ Action = (CancellationToken cancellationToken) => {
                var pathConfig = Utility.GetChannelPathConfig(0, 0);
                var pathCalibration = Utility.GetChannelPathCalibration(0, 0);
                Instruments.Instance.SetThunderscopeCalManual1M(0, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator);
                Utility.GetAndCheckSigGenZero(0, pathConfig, cancellationToken);
                return Sequencer.Status.Done;
            }},

            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L0", 0, 0),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L1", 0, 1),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L2", 0, 2),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L3", 0, 3),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L4", 0, 4),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L5", 0, 5),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L6", 0, 6),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L7", 0, 7),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L8", 0, 8),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L9", 0, 9),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - HG L10", 0, 10),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L0", 0, 11),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L1", 0, 12),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L2", 0, 13),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L3", 0, 14),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L4", 0, 15),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L5", 0, 16),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L6", 0, 17),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L7", 0, 18),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L8", 0, 19),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L9", 0, 20),
            new BufferInputVppStep("Channel 1 - measure buffer input Vpp - LG L10", 0, 21),
            new AttenuatorStep("Channel 1 - measure attenuator scale", 0),
            new PgaLoadStep("Channel 1 - measure PGA loading scales", 0),

            new Step("Disconnect SDG2042X"){ Action = (CancellationToken cancellationToken) => { Instruments.Instance.EnableSdgDc(-1); return Sequencer.Status.Done; }},

            new Step("Set channel 2"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetThunderscopeChannel([1]);
                return Sequencer.Status.Done; }},

            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L0", 1, 0),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L1", 1, 1),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L2", 1, 2),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L3", 1, 3),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L4", 1, 4),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L5", 1, 5),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L6", 1, 6),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L7", 1, 7),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L8", 1, 8),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L9", 1, 9),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - HG L10", 1, 10),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L0", 1, 11),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L1", 1, 12),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L2", 1, 13),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L3", 1, 14),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L4", 1, 15),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L5", 1, 16),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L6", 1, 17),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L7", 1, 18),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L8", 1, 19),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L9", 1, 20),
            new TrimOffsetDacGainStep("Channel 2 - measure trim offset DAC gain - LG L10", 1, 21),

            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L0", 1, 0),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L1", 1, 1),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L2", 1, 2),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L3", 1, 3),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L4", 1, 4),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L5", 1, 5),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L6", 1, 6),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L7", 1, 7),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L8", 1, 8),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L9", 1, 9),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - HG L10", 1, 10),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L0", 1, 11),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L1", 1, 12),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L2", 1, 13),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L3", 1, 14),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L4", 1, 15),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L5", 1, 16),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L6", 1, 17),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L7", 1, 18),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L8", 1, 19),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L9", 1, 20),
            new TrimOffsetDacZeroStep("Channel 2 - find trim offset DAC zero - LG L10", 1, 21),

            new Step("Connect SDG2042X"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.EnableSdgDc(1);
                cancellationToken.WaitHandle.WaitOne(2000);
                return Sequencer.Status.Done;
            }},
            new Step("Find SDG2042X zero"){ Action = (CancellationToken cancellationToken) => {
                var pathConfig = Utility.GetChannelPathConfig(1, 0);
                var pathCalibration = Utility.GetChannelPathCalibration(1, 0);
                Instruments.Instance.SetThunderscopeCalManual1M(1, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator);
                Utility.GetAndCheckSigGenZero(1, pathConfig, cancellationToken);
                return Sequencer.Status.Done;
            }},

            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L0", 1, 0),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L1", 1, 1),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L2", 1, 2),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L3", 1, 3),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L4", 1, 4),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L5", 1, 5),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L6", 1, 6),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L7", 1, 7),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L8", 1, 8),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L9", 1, 9),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - HG L10", 1, 10),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L0", 1, 11),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L1", 1, 12),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L2", 1, 13),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L3", 1, 14),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L4", 1, 15),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L5", 1, 16),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L6", 1, 17),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L7", 1, 18),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L8", 1, 19),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L9", 1, 20),
            new BufferInputVppStep("Channel 2 - measure buffer input Vpp - LG L10", 1, 21),
            new AttenuatorStep("Channel 2 - measure attenuator scale", 1),
            new PgaLoadStep("Channel 2 - measure PGA loading scales", 1),

            new Step("Disconnect SDG2042X"){ Action = (CancellationToken cancellationToken) => { Instruments.Instance.EnableSdgDc(-1); return Sequencer.Status.Done; }},

            new Step("Set Channel 3"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetThunderscopeChannel([2]);
                return Sequencer.Status.Done;
            }},

            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L0", 2, 0),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L1", 2, 1),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L2", 2, 2),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L3", 2, 3),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L4", 2, 4),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L5", 2, 5),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L6", 2, 6),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L7", 2, 7),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L8", 2, 8),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L9", 2, 9),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - HG L10", 2, 10),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L0", 2, 11),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L1", 2, 12),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L2", 2, 13),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L3", 2, 14),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L4", 2, 15),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L5", 2, 16),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L6", 2, 17),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L7", 2, 18),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L8", 2, 19),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L9", 2, 20),
            new TrimOffsetDacGainStep("Channel 3 - measure trim offset DAC gain - LG L10", 2, 21),

            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L0", 2, 0),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L1", 2, 1),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L2", 2, 2),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L3", 2, 3),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L4", 2, 4),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L5", 2, 5),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L6", 2, 6),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L7", 2, 7),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L8", 2, 8),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L9", 2, 9),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - HG L10", 2, 10),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L0", 2, 11),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L1", 2, 12),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L2", 2, 13),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L3", 2, 14),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L4", 2, 15),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L5", 2, 16),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L6", 2, 17),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L7", 2, 18),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L8", 2, 19),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L9", 2, 20),
            new TrimOffsetDacZeroStep("Channel 3 - find trim offset DAC zero - LG L10", 2, 21),

            new Step("Connect SDG2042X"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.EnableSdgDc(2);
                cancellationToken.WaitHandle.WaitOne(2000);
                return Sequencer.Status.Done;
            }},
            new Step("Find SDG2042X zero"){ Action = (CancellationToken cancellationToken) => {
                var pathConfig = Utility.GetChannelPathConfig(2, 0);
                var pathCalibration = Utility.GetChannelPathCalibration(2, 0);
                Instruments.Instance.SetThunderscopeCalManual1M(2, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator);
                Utility.GetAndCheckSigGenZero(2, pathConfig, cancellationToken);
                return Sequencer.Status.Done;
            }},

            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L0", 2, 0),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L1", 2, 1),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L2", 2, 2),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L3", 2, 3),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L4", 2, 4),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L5", 2, 5),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L6", 2, 6),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L7", 2, 7),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L8", 2, 8),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L9", 2, 9),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - HG L10", 2, 10),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L0", 2, 11),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L1", 2, 12),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L2", 2, 13),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L3", 2, 14),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L4", 2, 15),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L5", 2, 16),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L6", 2, 17),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L7", 2, 18),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L8", 2, 19),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L9", 2, 20),
            new BufferInputVppStep("Channel 3 - measure buffer input Vpp - LG L10", 2, 21),
            new AttenuatorStep("Channel 3 - measure attenuator scale", 2),
            new PgaLoadStep("Channel 3 - measure PGA loading scales", 2),

            new Step("Disconnect SDG2042X"){ Action = (CancellationToken cancellationToken) => { Instruments.Instance.EnableSdgDc(-1); return Sequencer.Status.Done; }},

            new Step("Set Channel 4"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetThunderscopeChannel([3]);
                return Sequencer.Status.Done;
            }},

            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L0", 3, 0),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L1", 3, 1),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L2", 3, 2),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L3", 3, 3),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L4", 3, 4),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L5", 3, 5),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L6", 3, 6),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L7", 3, 7),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L8", 3, 8),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L9", 3, 9),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - HG L10", 3, 10),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L0", 3, 11),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L1", 3, 12),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L2", 3, 13),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L3", 3, 14),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L4", 3, 15),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L5", 3, 16),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L6", 3, 17),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L7", 3, 18),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L8", 3, 19),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L9", 3, 20),
            new TrimOffsetDacGainStep("Channel 4 - measure trim offset DAC gain - LG L10", 3, 21),

            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L0", 3, 0),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L1", 3, 1),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L2", 3, 2),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L3", 3, 3),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L4", 3, 4),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L5", 3, 5),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L6", 3, 6),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L7", 3, 7),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L8", 3, 8),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L9", 3, 9),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - HG L10", 3, 10),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L0", 3, 11),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L1", 3, 12),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L2", 3, 13),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L3", 3, 14),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L4", 3, 15),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L5", 3, 16),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L6", 3, 17),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L7", 3, 18),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L8", 3, 19),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L9", 3, 20),
            new TrimOffsetDacZeroStep("Channel 4 - find trim offset DAC zero - LG L10", 3, 21),

            new Step("Connect SDG2042X"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.EnableSdgDc(3);
                cancellationToken.WaitHandle.WaitOne(2000);
                return Sequencer.Status.Done;
            }},
            new Step("Find SDG2042X zero"){ Action = (CancellationToken cancellationToken) => {
                var pathConfig = Utility.GetChannelPathConfig(3, 0);
                var pathCalibration = Utility.GetChannelPathCalibration(3, 0);
                Instruments.Instance.SetThunderscopeCalManual1M(3, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator);
                Utility.GetAndCheckSigGenZero(3, pathConfig, cancellationToken);
                return Sequencer.Status.Done;
            }},

            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L0", 3, 0),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L1", 3, 1),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L2", 3, 2),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L3", 3, 3),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L4", 3, 4),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L5", 3, 5),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L6", 3, 6),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L7", 3, 7),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L8", 3, 8),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L9", 3, 9),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - HG L10", 3, 10),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L0", 3, 11),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L1", 3, 12),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L2", 3, 13),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L3", 3, 14),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L4", 3, 15),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L5", 3, 16),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L6", 3, 17),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L7", 3, 18),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L8", 3, 19),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L9", 3, 20),
            new BufferInputVppStep("Channel 4 - measure buffer input Vpp - LG L10", 3, 21),
            new AttenuatorStep("Channel 4 - measure attenuator scale", 3),
            new PgaLoadStep("Channel 4 - measure PGA loading scales", 3),

            new Step("Disconnect SDG2042X"){ Action = (CancellationToken cancellationToken) => { Instruments.Instance.EnableSdgDc(-1); return Sequencer.Status.Done; }},

            new Step("Save calibration file"){ Action = (CancellationToken cancellationToken) => {
                Variables.Instance.Calibration.ToJsonFile(Variables.Instance.CalibrationFileName);
                return Sequencer.Status.Done;
            }},

            new Step("Cleanup"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.Close();
                return Sequencer.Status.Done;
            }},
        ];
        SetIndices();
    }
}
