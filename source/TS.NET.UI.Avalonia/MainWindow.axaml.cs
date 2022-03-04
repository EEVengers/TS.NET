using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Cloudtoid.Interprocess;
using ScottPlot;
using ScottPlot.Avalonia;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TS.NET.UI.Avalonia
{
    public partial class MainWindow : Window
    {
        private AvaPlot avaPlot1;
        private Label lblStatus;
        private double[] channel1 = null;
        private double[] channel2 = null;
        private double[] channel3 = null;
        private double[] channel4 = null;
        private ScottPlot.Plottable.HLine triggerLine;
        private CancellationTokenSource cancellationTokenSource;
        private Task displayTask;

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            channel1 = new double[1000];
            channel2 = new double[1000];
            channel3 = new double[1000];
            channel4 = new double[1000];

            avaPlot1 = this.Find<AvaPlot>("AvaPlot1");
            lblStatus = this.Find<Label>("LblStatus");
            avaPlot1.Plot.Style(Style.Gray2);
            avaPlot1.Plot.Legend(true, Alignment.LowerRight);
            avaPlot1.Plot.AddSignal(channel1, 1, null, "Ch1");
            avaPlot1.Plot.AddSignal(channel2, 1, null, "Ch2");
            avaPlot1.Plot.AddSignal(channel3, 1, null, "Ch3");
            avaPlot1.Plot.AddSignal(channel4, 1, null, "Ch4");
            avaPlot1.Plot.XAxis.Label("Time (ns)");
            avaPlot1.Plot.YAxis.Label("ADC reading");
            avaPlot1.Plot.SetAxisLimitsY(0, 255);
            avaPlot1.Plot.SetAxisLimitsX(0, 1000);
            triggerLine = avaPlot1.Plot.AddHorizontalLine(200, System.Drawing.Color.White, 2, LineStyle.Dash);

            //using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            cancellationTokenSource = new();
            displayTask = Task.Factory.StartNew(() => UpdateChart(cancellationTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        private void UpdateChart(CancellationToken cancelToken)
        {
            try
            {
                // This is an inter-process high-performance queue using memory-mapped file & semaphore. Used here to get data from the simulator or hardware.
                var queueFactory = new QueueFactory();
                var queueOptions = new QueueOptions(queueName: "ThunderScopeTriggeredCaptureForwarder", bytesCapacity: 2 * 8000000);
                using var forwarder = queueFactory.CreateSubscriber(queueOptions);

                int count = 0;
                while (true)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    if (forwarder.TryDequeue(cancelToken, out string dtoName, out ReadOnlyMemory<byte> payload))
                    {
                        switch (dtoName)
                        {
                            case nameof(TriggeredCaptureDto):
                                var dto = payload.Deserialise<TriggeredCaptureDto>();
                                var channel1Data = dto.ChannelData.Slice(0, dto.ChannelLength).Span;
                                var channel2Data = dto.ChannelData.Slice(dto.ChannelLength, dto.ChannelLength).Span;
                                var channel3Data = dto.ChannelData.Slice(dto.ChannelLength * 2, dto.ChannelLength).Span;
                                var channel4Data = dto.ChannelData.Slice(dto.ChannelLength * 3, dto.ChannelLength).Span;
                                for (int i = 0; i < dto.ChannelLength; i++)
                                {
                                    channel1[i] = channel1Data[i];
                                    channel2[i] = channel2Data[i];
                                    channel3[i] = channel3Data[i];
                                    channel4[i] = channel4Data[i];
                                }
                                count++;
                                Dispatcher.UIThread.InvokeAsync(() => { avaPlot1.Render(); lblStatus.Content = $"Count: {count}"; });
                                break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //logger.LogDebug("TriggeredCaptureForwarderTask stopping");
                throw;
            }
            catch (Exception ex)
            {
                //logger.LogCritical(ex, "TriggeredCaptureForwarderTask error");
                throw;
            }
            finally
            {
                //logger.LogDebug("TriggeredCaptureForwarderTask stopped");
            }
        }
    }
}
