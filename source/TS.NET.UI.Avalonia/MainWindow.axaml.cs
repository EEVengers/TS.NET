using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Cloudtoid.Interprocess;
using Microsoft.Extensions.Logging;
using ScottPlot;
using ScottPlot.Avalonia;
using System;
using System.Buffers;
using System.Diagnostics;
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
        //private IPublisher forwarderInput;
        //private Memory<byte> forwarderInputBuffer = new byte[10000];
        private ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif

            //var queueFactory = new QueueFactory();
            //var queueOptions = new QueueOptions(queueName: "ThunderScopeTriggeredCaptureForwarderInput", bytesCapacity: 2 * 8000000);
            //forwarderInput = queueFactory.CreatePublisher(queueOptions);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            channel1 = ArrayPool<double>.Shared.Rent(1);
            channel2 = ArrayPool<double>.Shared.Rent(1);
            channel3 = ArrayPool<double>.Shared.Rent(1);
            channel4 = ArrayPool<double>.Shared.Rent(1);

            avaPlot1 = this.Find<AvaPlot>("AvaPlot1");
            lblStatus = this.Find<Label>("LblStatus");
            avaPlot1.Plot.Style(Style.Gray2);
            avaPlot1.Plot.Legend(true, Alignment.LowerRight);
            ResetSeries();
            avaPlot1.Plot.XAxis.Label("Time (ns)");
            avaPlot1.Plot.YAxis.Label("ADC reading");
            avaPlot1.Plot.SetAxisLimitsY(0, 255);
            avaPlot1.Plot.SetAxisLimitsX(0, 1000);
            triggerLine = avaPlot1.Plot.AddHorizontalLine(200, System.Drawing.Color.White, 2, LineStyle.Dash);

            //using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            cancellationTokenSource = new();
            displayTask = Task.Factory.StartNew(() => UpdateChart(cancellationTokenSource.Token), TaskCreationOptions.LongRunning);
        }

        private void ResetSeries()
        {
            avaPlot1.Plot.Clear();
            avaPlot1.Plot.AddSignal(channel1, 1, null, "Ch1");
            avaPlot1.Plot.AddSignal(channel2, 1, null, "Ch2");
            avaPlot1.Plot.AddSignal(channel3, 1, null, "Ch3");
            avaPlot1.Plot.AddSignal(channel4, 1, null, "Ch4");
        }

        private unsafe void UpdateChart(CancellationToken cancelToken)
        {
            try
            {
                Postbox postbox = new(new PostboxOptions("ThunderScope.1", 4 * 10 * 1000000), loggerFactory);
                var postboxReadSemaphore = postbox.GetReaderSemaphore();
                int channelLength = 10 * 1000000;
                int viewportLength = 10000;
                Stopwatch stopwatch = Stopwatch.StartNew();

                int count = 0;
                while (true)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    if (postboxReadSemaphore.Wait(500))
                    {
                        if (channel1.Length != viewportLength)
                        {
                            channel1 = new double[viewportLength];
                            ResetSeries();
                        }
                        if (channel2.Length != viewportLength)
                        {
                            channel2 = new double[viewportLength];
                            ResetSeries();
                        }
                        if (channel3.Length != viewportLength)
                        {
                            channel3 = new double[viewportLength];
                            ResetSeries();
                        }
                        if (channel4.Length != viewportLength)
                        {
                            channel4 = new double[viewportLength];
                            ResetSeries();
                        }

                        PointerExtensions.ExtendToDouble(postbox.DataPointer, viewportLength, channel1);
                        PointerExtensions.ExtendToDouble(postbox.DataPointer + channelLength, viewportLength, channel2);
                        PointerExtensions.ExtendToDouble(postbox.DataPointer + channelLength + channelLength, viewportLength, channel3);
                        PointerExtensions.ExtendToDouble(postbox.DataPointer + channelLength + channelLength + channelLength, viewportLength, channel4);
                        postbox.DataIsRead();

                        count++;
                        Dispatcher.UIThread.InvokeAsync(() => { avaPlot1.Render(); lblStatus.Content = $"Count: {count}"; });
                        stopwatch.Restart();
                        Thread.Sleep(100);
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
