using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class NoiseVerificationSequence : Sequence
{
    public NoiseVerificationVariables Variables { get; private set; }

    public NoiseVerificationSequence(ModalUiContext modalUiContext, NoiseVerificationVariables variables)
    {
        Name = "Noise verification";
        Variables = variables;
        AddSteps(modalUiContext);
        SetStepIndices();
    }

    private void AddSteps(ModalUiContext modalUiContext)
    {
        Steps =
        [
            new ModalDialogStep("Cable check", modalUiContext)
            {
                Title = "Cable check",
                Message = "Are all cables disconnected from channels 1-4?",
                Buttons = DialogButtons.YesNo,
                Icon = DialogIcon.Question
            },
            new InitialiseDeviceStep("Initialise device", Variables),
            new LoadUserCalFromDeviceStep("Load calibration from device", Variables),
            new Step("Load ADC branch gains"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.SetThunderscopeAdcCalibration(Variables.Calibration.Adc.ToDriver());
                return Sequencer.Status.Done;
            }},
            new WarmupStep("Warmup device", Variables) { Skip = false, AllowSkip = true },

            new AcRmsStep("Channel 1 - AC RMS - 50R, 8-bit, 1 GSPS, BW 20M, PGA HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            new AcRmsStep("Channel 2 - AC RMS - 50R, 8-bit, 1 GSPS, BW 20M, PGA HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            new AcRmsStep("Channel 3 - AC RMS - 50R, 8-bit, 1 GSPS, BW 20M, PGA HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            new AcRmsStep("Channel 4 - AC RMS - 50R, 8-bit, 1 GSPS, BW 20M, PGA HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw20M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            
            new AcRmsStep("Channel 1 - AC RMS - 50R, 8-bit, 1 GSPS, BW 100M, PGA HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            new AcRmsStep("Channel 2 - AC RMS - 50R, 8-bit, 1 GSPS, BW 100M, PGA HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            new AcRmsStep("Channel 3 - AC RMS - 50R, 8-bit, 1 GSPS, BW 100M, PGA HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            new AcRmsStep("Channel 4 - AC RMS - 50R, 8-bit, 1 GSPS, BW 100M, PGA HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw100M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            
            new AcRmsStep("Channel 1 - AC RMS - 50R, 8-bit, 1 GSPS, BW 200M, PGA HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw200M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            new AcRmsStep("Channel 2 - AC RMS - 50R, 8-bit, 1 GSPS, BW 200M, PGA HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw200M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            new AcRmsStep("Channel 3 - AC RMS - 50R, 8-bit, 1 GSPS, BW 200M, PGA HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw200M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            new AcRmsStep("Channel 4 - AC RMS - 50R, 8-bit, 1 GSPS, BW 200M, PGA HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw200M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            
            new AcRmsStep("Channel 1 - AC RMS - 50R, 8-bit, 1 GSPS, BW 350M, PGA HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            new AcRmsStep("Channel 2 - AC RMS - 50R, 8-bit, 1 GSPS, BW 350M, PGA HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            new AcRmsStep("Channel 3 - AC RMS - 50R, 8-bit, 1 GSPS, BW 350M, PGA HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            new AcRmsStep("Channel 4 - AC RMS - 50R, 8-bit, 1 GSPS, BW 350M, PGA HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.Bw350M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            
            new AcRmsStep("Channel 1 - AC RMS - 50R, 8-bit, 1 GSPS, BW FULL, PGA HG L0", 0, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            new AcRmsStep("Channel 2 - AC RMS - 50R, 8-bit, 1 GSPS, BW FULL, PGA HG L0", 1, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            new AcRmsStep("Channel 3 - AC RMS - 50R, 8-bit, 1 GSPS, BW FULL, PGA HG L0", 2, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },
            new AcRmsStep("Channel 4 - AC RMS - 50R, 8-bit, 1 GSPS, BW FULL, PGA HG L0", 3, 0, ThunderscopeTermination.FiftyOhm, ThunderscopeBandwidth.BwFull, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00009, Averages = 100 },

            new AcRmsStep("Channel 1 - AC RMS - 1M, 8-bit, 1 GSPS, BW 20M, PGA HG L0", 0, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.Bw20M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },
            new AcRmsStep("Channel 2 - AC RMS - 1M, 8-bit, 1 GSPS, BW 20M, PGA HG L0", 1, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.Bw20M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },
            new AcRmsStep("Channel 3 - AC RMS - 1M, 8-bit, 1 GSPS, BW 20M, PGA HG L0", 2, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.Bw20M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },
            new AcRmsStep("Channel 4 - AC RMS - 1M, 8-bit, 1 GSPS, BW 20M, PGA HG L0", 3, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.Bw20M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },
            
            new AcRmsStep("Channel 1 - AC RMS - 1M, 8-bit, 1 GSPS, BW 100M, PGA HG L0", 0, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.Bw100M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },
            new AcRmsStep("Channel 2 - AC RMS - 1M, 8-bit, 1 GSPS, BW 100M, PGA HG L0", 1, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.Bw100M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },
            new AcRmsStep("Channel 3 - AC RMS - 1M, 8-bit, 1 GSPS, BW 100M, PGA HG L0", 2, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.Bw100M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },
            new AcRmsStep("Channel 4 - AC RMS - 1M, 8-bit, 1 GSPS, BW 100M, PGA HG L0", 3, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.Bw100M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },
            
            new AcRmsStep("Channel 1 - AC RMS - 1M, 8-bit, 1 GSPS, BW 200M, PGA HG L0", 0, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.Bw200M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },
            new AcRmsStep("Channel 2 - AC RMS - 1M, 8-bit, 1 GSPS, BW 200M, PGA HG L0", 1, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.Bw200M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },
            new AcRmsStep("Channel 3 - AC RMS - 1M, 8-bit, 1 GSPS, BW 200M, PGA HG L0", 2, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.Bw200M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },
            new AcRmsStep("Channel 4 - AC RMS - 1M, 8-bit, 1 GSPS, BW 200M, PGA HG L0", 3, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.Bw200M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },
            
            new AcRmsStep("Channel 1 - AC RMS - 1M, 8-bit, 1 GSPS, BW 350M, PGA HG L0", 0, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.Bw350M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },
            new AcRmsStep("Channel 2 - AC RMS - 1M, 8-bit, 1 GSPS, BW 350M, PGA HG L0", 1, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.Bw350M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },
            new AcRmsStep("Channel 3 - AC RMS - 1M, 8-bit, 1 GSPS, BW 350M, PGA HG L0", 2, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.Bw350M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },
            new AcRmsStep("Channel 4 - AC RMS - 1M, 8-bit, 1 GSPS, BW 350M, PGA HG L0", 3, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.Bw350M, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },
            
            new AcRmsStep("Channel 1 - AC RMS - 1M, 8-bit, 1 GSPS, BW FULL, PGA HG L0", 0, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.BwFull, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },
            new AcRmsStep("Channel 2 - AC RMS - 1M, 8-bit, 1 GSPS, BW FULL, PGA HG L0", 1, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.BwFull, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },
            new AcRmsStep("Channel 3 - AC RMS - 1M, 8-bit, 1 GSPS, BW FULL, PGA HG L0", 2, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.BwFull, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },
            new AcRmsStep("Channel 4 - AC RMS - 1M, 8-bit, 1 GSPS, BW FULL, PGA HG L0", 3, 0, ThunderscopeTermination.OneMegaohm, ThunderscopeBandwidth.BwFull, 1_000_000_000, Variables){ MinLimit = 0.00003, MaxLimit = 0.00012, Averages = 100 },

            new NsdStep("Channel 1 - NSD - 50R, 8-bit, 1 GSPS, BW FULL, PGA HG L0", Variables)
            {
                ChannelIndex = 0,
                Termination = ThunderscopeTermination.FiftyOhm,
                PgaPreampGain = PgaPreampGain.High,
                PgaLadder = 0,
                Bandwidth = ThunderscopeBandwidth.BwFull,
                SampleRateHz = 1_000_000_000,
            },
            new NsdStep("Channel 2 - NSD - 50R, 8-bit, 1 GSPS, BW FULL, PGA HG L0", Variables)
            {
                ChannelIndex = 1,
                Termination = ThunderscopeTermination.FiftyOhm,
                PgaPreampGain = PgaPreampGain.High,
                PgaLadder = 0,
                Bandwidth = ThunderscopeBandwidth.BwFull,
                SampleRateHz = 1_000_000_000,
            },
            new NsdStep("Channel 3 - NSD - 50R, 8-bit, 1 GSPS, BW FULL, PGA HG L0", Variables)
            {
                ChannelIndex = 2,
                Termination = ThunderscopeTermination.FiftyOhm,
                PgaPreampGain = PgaPreampGain.High,
                PgaLadder = 0,
                Bandwidth = ThunderscopeBandwidth.BwFull,
                SampleRateHz = 1_000_000_000,
            },
            new NsdStep("Channel 4 - NSD - 50R, 8-bit, 1 GSPS, BW FULL, PGA HG L0", Variables)
            {
                ChannelIndex = 3,
                Termination = ThunderscopeTermination.FiftyOhm,
                PgaPreampGain = PgaPreampGain.High,
                PgaLadder = 0,
                Bandwidth = ThunderscopeBandwidth.BwFull,
                SampleRateHz = 1_000_000_000,
            },
            new CombineChartsStep("Combined - NSD - 50R, 8-bit, 1 GSPS, BW FULL, PGA HG L0", this),

            new Step("Cleanup"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.Close();
                return Sequencer.Status.Done;
            }},
        ];
    }

    private class CombineChartsStep : Step
    {
        public CombineChartsStep(string name, Sequence sequence) : base(name)
        {
            Action = (CancellationToken cancellationToken) =>
            {
                var channel1 = (ResultMetadataXYChart)sequence.Steps!.Where(s => s.Name == "Channel 1 - NSD - 50R, 8-bit, 1 GSPS, BW FULL, PGA HG L0").First().Result!.Metadata!.Where(r => r.GetType() == typeof(ResultMetadataXYChart)).First();
                var channel2 = (ResultMetadataXYChart)sequence.Steps!.Where(s => s.Name == "Channel 2 - NSD - 50R, 8-bit, 1 GSPS, BW FULL, PGA HG L0").First().Result!.Metadata!.Where(r => r.GetType() == typeof(ResultMetadataXYChart)).First();
                var channel3 = (ResultMetadataXYChart)sequence.Steps!.Where(s => s.Name == "Channel 3 - NSD - 50R, 8-bit, 1 GSPS, BW FULL, PGA HG L0").First().Result!.Metadata!.Where(r => r.GetType() == typeof(ResultMetadataXYChart)).First();
                var channel4 = (ResultMetadataXYChart)sequence.Steps!.Where(s => s.Name == "Channel 4 - NSD - 50R, 8-bit, 1 GSPS, BW FULL, PGA HG L0").First().Result!.Metadata!.Where(r => r.GetType() == typeof(ResultMetadataXYChart)).First();

                var metadata =
                    new ResultMetadataXYChart()
                    {
                        ShowInReport = true,
                        Title = $"Noise spectral density",
                        XAxis = new ResultMetadataXYChartAxis { Label = "Frequency (Hz)", Scale = XYChartScaleType.Log10, AdditionalRangeValues = [10e3, 1e9] },
                        YAxis = new ResultMetadataXYChartAxis { Label = "Noise (V/rHz)", Scale = XYChartScaleType.Log10, AdditionalRangeValues = [100e-9, 100e-12] },
                        LegendLocation = ResultMetadataXYChartLegendLocation.BottomLeft,
                        Series =
                        [
                            channel1.Series.First(),
                            channel2.Series.First(),
                            channel3.Series.First(),
                            channel4.Series.First()
                        ]
                    };

                Result!.Metadata!.Add(metadata);

                return Sequencer.Status.Done;
            };
        }
    }
}
