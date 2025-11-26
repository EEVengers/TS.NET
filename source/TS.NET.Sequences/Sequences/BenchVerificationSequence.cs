using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class BenchVerificationSequence : Sequence
{
    public BenchVerificationVariables Variables { get; private set; }

    public BenchVerificationSequence(ModalUiContext modalUiContext, BenchVerificationVariables variables)
    {
        Name = "Bench verification";
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
                Message = "Are cables connected from 2x signal generators to channel 1-4?",
                Buttons = DialogButtons.YesNo, 
                Icon = DialogIcon.Question
            },
            new InitialiseDeviceStep("Initialise device", Variables),
            new InitialiseSigGensStep("Initialise signal generators", Variables),
            new LoadUserCalFromDeviceStep("Load calibration from device", Variables),
            new Step("Load ADC branch gains")
            { 
                Action = (CancellationToken cancellationToken) =>
                {
                    Instruments.Instance.SetThunderscopeAdcCalibration(Variables.Calibration.Adc.ToDriver());
                    return Sequencer.Status.Done;
                }
            },
            new WarmupStep("Warmup device", Variables)
            { 
                Skip = false, 
                AllowSkip = true 
            },

            new BodePlotStep("Channel 1 - Crossover flatness", Variables)
            {
                ChannelIndex = 0,
                ChannelsEnabled = [0],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 10,
                Attenuator = false,
                SampleRateHz = 1_000_000_000,
                MaxFrequency = 10_000_000
            },
            new BodePlotStep("Channel 2 - Crossover flatness", Variables)
            {
                ChannelIndex = 1,
                ChannelsEnabled = [1],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 10,
                Attenuator = false,
                SampleRateHz = 1_000_000_000,
                MaxFrequency = 10_000_000
            },
            new BodePlotStep("Channel 3 - Crossover flatness", Variables)
            {
                ChannelIndex = 2,
                ChannelsEnabled = [2],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 10,
                Attenuator = false,
                SampleRateHz = 1_000_000_000,
                MaxFrequency = 10_000_000
            },
            new BodePlotStep("Channel 4 - Crossover flatness", Variables)
            {
                ChannelIndex = 3,
                ChannelsEnabled = [3],
                PgaPreampGain = PgaPreampGain.Low,
                PgaLadder = 10,
                Attenuator = false,
                SampleRateHz = 1_000_000_000,
                MaxFrequency = 10_000_000
            },

            new CombinedSeriesStep("Combined - Crossover flatness", this)
            {
                ChartTitle = "Overall gain vs. frequency",
                ChartXAxis = new ResultMetadataXYChartAxis
                { 
                    Label = "Frequency (Hz)", 
                    Scale = XYChartScaleType.Log10 
                },
                ChartYAxis = new ResultMetadataXYChartAxis
                { 
                    Label = "Gain (dB)", 
                    Scale = XYChartScaleType.Linear, 
                    AdditionalRangeValues = [-0.5, 0.5] 
                },
                ChartSeries = [
                    new SeriesReference(){StepName = "Channel 1 - Crossover flatness"},
                    new SeriesReference(){StepName = "Channel 2 - Crossover flatness"},
                    new SeriesReference(){StepName = "Channel 3 - Crossover flatness"},
                    new SeriesReference(){StepName = "Channel 4 - Crossover flatness"}
                ],
                ChartLegendLocation = ResultMetadataXYChartLegendLocation.BottomLeft,
            },

            // Note: SDG has 5 Vpp limit for frequencies above 10 MHz, use correct pregamp gain & ladder setting.
            new BodePlotStep("Channel 1 - Attenuator flatness", Variables)
            {
                ChannelIndex = 0,
                ChannelsEnabled = [0],
                PgaPreampGain = PgaPreampGain.High,
                PgaLadder = 10,
                Attenuator = true,
                SampleRateHz = 1_000_000_000,
                MaxFrequency = 10_000_000
            },
            new BodePlotStep("Channel 2 - Attenuator flatness", Variables)
            {
                ChannelIndex = 1,
                ChannelsEnabled = [1],
                PgaPreampGain = PgaPreampGain.High,
                PgaLadder = 10,
                Attenuator = true,
                SampleRateHz = 1_000_000_000,
                MaxFrequency = 10_000_000
            },
            new BodePlotStep("Channel 3 - Attenuator flatness", Variables)
            {
                ChannelIndex = 2,
                ChannelsEnabled = [2],
                PgaPreampGain = PgaPreampGain.High,
                PgaLadder = 10,
                Attenuator = true,
                SampleRateHz = 1_000_000_000,
                MaxFrequency = 10_000_000
            },
            new BodePlotStep("Channel 4 - Attenuator flatness", Variables)
            {
                ChannelIndex = 3,
                ChannelsEnabled = [3],
                PgaPreampGain = PgaPreampGain.High,
                PgaLadder = 10,
                Attenuator = true,
                SampleRateHz = 1_000_000_000,
                MaxFrequency = 10_000_000
            },

            new CombinedSeriesStep("Combined - Attenuator flatness", this)
            {
                ChartTitle = "Overall gain vs. frequency",
                ChartXAxis = new ResultMetadataXYChartAxis
                { 
                    Label = "Frequency (Hz)", 
                    Scale = XYChartScaleType.Log10 
                },
                ChartYAxis = new ResultMetadataXYChartAxis
                { 
                    Label = "Gain (dB)", 
                    Scale = XYChartScaleType.Linear, 
                    AdditionalRangeValues = [-0.5, 0.5]
                },
                ChartSeries = [
                    new SeriesReference { StepName = "Channel 1 - Attenuator flatness" },
                    new SeriesReference { StepName = "Channel 2 - Attenuator flatness" },
                    new SeriesReference { StepName = "Channel 3 - Attenuator flatness" },
                    new SeriesReference { StepName = "Channel 4 - Attenuator flatness" }
                ],
                ChartLegendLocation = ResultMetadataXYChartLegendLocation.BottomLeft,
            },

            new Step("Cleanup")
            { 
                Action = (CancellationToken cancellationToken) => 
                {
                    Instruments.Instance.Close();
                    SigGens.Instance.Close();
                    return Sequencer.Status.Done;
                }
            },
        ];
    }
}
