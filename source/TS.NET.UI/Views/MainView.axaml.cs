using Avalonia.Controls;
using ScottPlot;
using System.Threading.Tasks;
using System.Threading;
using TS.NET.UI.ViewModels;
using Avalonia.Threading;
using System.Diagnostics;
using System;
using System.Buffers;
using Avalonia.Interactivity;
using System.Text.Json;
using Newtonsoft.Json;

namespace TS.NET.UI.Views;

public partial class MainView : UserControl
{
    private double[] channel1 = null;
    private double[] channel2 = null;
    private double[] channel3 = null;
    private double[] channel4 = null;
    private ScottPlot.Plottable.HLine triggerLevel;
    private ScottPlot.Plottable.VLine triggerDelay;
    private readonly CancellationTokenSource cancellationTokenSource;
    private readonly Task displayTask;
    private readonly SemaphoreSlim renderSemaphore = new SemaphoreSlim(1);

    public MainView()
    {
        InitializeComponent();
        InitializeEvents();

        channel1 = ArrayPool<double>.Shared.Rent(1);
        channel2 = ArrayPool<double>.Shared.Rent(1);
        channel3 = ArrayPool<double>.Shared.Rent(1);
        channel4 = ArrayPool<double>.Shared.Rent(1);

        avaPlot1.Plot.Legend(true, Alignment.LowerRight);
        ResetSeries();
        avaPlot1.Plot.XAxis.Label("Time (s)");
        avaPlot1.Plot.YAxis.Label("ADC reading");
        avaPlot1.Plot.SetAxisLimitsX(0, 1);
        avaPlot1.Plot.SetAxisLimitsY(-128, 127);
        triggerLevel = avaPlot1.Plot.AddHorizontalLine(0, System.Drawing.Color.Black, 1, LineStyle.Dash);
        triggerDelay = avaPlot1.Plot.AddVerticalLine(0, System.Drawing.Color.Black, 1, LineStyle.Solid);

        avaPlot1.Configuration.UseRenderQueue = false;      // Blocking RenderRequest?

        //using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        cancellationTokenSource = new();
        displayTask = Task.Factory.StartNew(() => UpdateChart(cancellationTokenSource.Token), TaskCreationOptions.LongRunning);      
    }

    private void InitializeEvents()
    {
        textScpiInput.KeyDown += TextScpiInput_KeyDown;
        sliderVert.ValueChanged += SliderVert_ValueChanged;
        sliderHorz.ValueChanged += SliderHorz_ValueChanged;
        textScpiConsole.SizeChanged += TextScpiConsole_SizeChanged;
    }

    private void TextScpiInput_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
        {
            var mainViewModel = DataContext as MainViewModel;
            mainViewModel?.ScpiInputOnEnter();
        }
    }

    private void SliderVert_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        double voltage = 0.4 * sliderVert.Value;
        var mainViewModel = DataContext as MainViewModel;
        mainViewModel?.TriggerLevelChange(voltage);
    }

    private void SliderHorz_ValueChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        ulong fsDelay = (ulong)(10000000000000 * sliderHorz.Value);
        var mainViewModel = DataContext as MainViewModel;
        mainViewModel?.TriggerDelayChange(fsDelay);
    }

    private void TextScpiConsole_SizeChanged(object? sender, SizeChangedEventArgs e)
    {
        scrollScpiConsole.ScrollToEnd();
    }

    private void ResetSeries()
    {
        avaPlot1.Plot.Clear();
        avaPlot1.Plot.AddSignal(channel1, 250000000, null, "Ch1");
        avaPlot1.Plot.AddSignal(channel2, 250000000, null, "Ch2");
        avaPlot1.Plot.AddSignal(channel3, 250000000, null, "Ch3");
        avaPlot1.Plot.AddSignal(channel4, 250000000, null, "Ch4");
        triggerLevel = avaPlot1.Plot.AddHorizontalLine(0, System.Drawing.Color.Black, 1, LineStyle.Dash);
        triggerDelay = avaPlot1.Plot.AddVerticalLine(0, System.Drawing.Color.Black, 1, LineStyle.Solid);
        //avaPlot1.Plot.SetAxisLimitsY(-128, 127);
        //avaPlot1.Plot.YAxis.LockLimits();
    }

    private void UpdateChart(CancellationToken cancelToken)
    {
        try
        {
            uint bufferLength = 4 * 100 * 1000 * 1000;      //Maximum record length = 100M samples per channel
            ThunderscopeDataBridgeReader bridge = new("ThunderScope.0");
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                lblStatus.Content = "Bridge connection established";
            });
            Stopwatch stopwatch = Stopwatch.StartNew();

            int count = 0;
            while (true)
            {
                cancelToken.ThrowIfCancellationRequested();
                renderSemaphore.Wait();
                if (bridge.RequestAndWaitForData(500))
                {
                    ulong channelLength = bridge.Processing.CurrentChannelDataLength;
                    uint viewportLength = (uint)bridge.Processing.CurrentChannelDataLength;
                    //uint viewportLength = 1000000;//

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

                    var cfg = bridge.Hardware;
                    var status = $"[Horizontal] Displaying {AddPrefix(viewportLength)} samples of {AddPrefix(channelLength)} [Acquisitions] displayed: {bridge.Monitoring.Processing.BridgeReads}, missed: {bridge.Monitoring.Processing.BridgeWrites - bridge.Monitoring.Processing.BridgeReads}, total: {bridge.Monitoring.Processing.BridgeWrites}";
                    var data = bridge.AcquiredDataRegionI8;
                    int offset = (int)((channelLength / 2) - (viewportLength / 2));
                    data.Slice(offset, (int)viewportLength).ToDoubleArray(channel1); offset += (int)channelLength;
                    data.Slice(offset, (int)viewportLength).ToDoubleArray(channel2); offset += (int)channelLength;
                    data.Slice(offset, (int)viewportLength).ToDoubleArray(channel3); offset += (int)channelLength;
                    data.Slice(offset, (int)viewportLength).ToDoubleArray(channel4);
                    triggerLevel.Y = bridge.Processing.TriggerLevel;
                    triggerDelay.X = ((double)bridge.Processing.TriggerDelayFs)/1000000000000000.0;

                    //var reading = bridge.Span[(int)upDownIndex.Value];
                    count++;
                    string textInfo = JsonConvert.SerializeObject(bridge.Processing, Formatting.Indented, new Newtonsoft.Json.Converters.StringEnumConverter());
                    textInfo += JsonConvert.SerializeObject(bridge.Hardware, Formatting.Indented, new Newtonsoft.Json.Converters.StringEnumConverter());
                    //var options = new JsonSerializerOptions { WriteIndented = true };
                    //string textInfo = JsonSerializer.Serialize(bridge.Hardware, options); 
                    //textInfo += JsonSerializer.Serialize(bridge.Processing, options);
                    //@$"Channels: {cfg.Channels}
                    //Channel length: {cfg.ChannelLength}
                    //Trigger channel: {cfg.TriggerChannel}
                    //Trigger mode: {cfg.TriggerMode}

                    //Channel 1:
                    //DC coupling
                    //20MHz bandwidth";

                    Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        //textBlockInfo.Text = textInfo;
                        var mainViewModel = DataContext as MainViewModel;
                        mainViewModel.Info = textInfo;
                        lblStatus.Content = status;
                        avaPlot1.RenderRequest(RenderType.LowQuality);  // With Configuration.UseRenderQueue = false, this should be a blocking call
                        renderSemaphore.Release();
                    });


                    stopwatch.Restart();
                    //Thread.Sleep(1000);
                }
                else
                    renderSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            //logger.LogDebug("UpdateChart stopping");
            throw;
        }
        catch (Exception ex)
        {
            //logger.LogCritical(ex, "UpdateChart error");
            throw;
        }
        finally
        {
            //logger.LogDebug("UpdateChart stopped");
        }
    }

    public static string AddPrefix(double value, string unit = "")
    {
        string[] superSuffix = new string[] { "K", "M", "G", "T", "P", "A", };
        string[] subSuffix = new string[] { "m", "u", "n", "p", "f", "a" };
        double v = value;
        int exp = 0;
        while (v - Math.Floor(v) > 0)
        {
            if (exp >= 18)
                break;
            exp += 3;
            v *= 1000;
            v = Math.Round(v, 12);
        }

        while (Math.Floor(v).ToString().Length > 3)
        {
            if (exp <= -18)
                break;
            exp -= 3;
            v /= 1000;
            v = Math.Round(v, 12);
        }
        if (exp > 0)
            return v.ToString() + subSuffix[exp / 3 - 1] + unit;
        else if (exp < 0)
            return v.ToString() + superSuffix[-exp / 3 - 1] + unit;
        return v.ToString() + unit;
    }
}
