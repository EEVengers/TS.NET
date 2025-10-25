using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class Instruments
{
    private static readonly Lazy<Instruments> lazy = new(() => new Instruments());
    public static Instruments Instance { get { return lazy.Value; } }
    private Instruments() { }

    //private ThunderscopeScpiConnection? thunderScope;
    private Driver.Libtslitex.Thunderscope? thunderScope;
    private ThunderscopeDataConnection? thunderScopeData;
    private TcpScpiConnection? sigGen1;
    private TcpScpiConnection? sigGen2;

    //private const int acquisitionRegionCount = 3;
    private ThunderscopeMemoryRegion? memoryRegion;
    private ThunderscopeMemoryRegion? shuffleRegion;

    private uint cachedSampleRateHz = 0;

    public void InitialiseThunderscope()
    {
        // ThunderScope
        //thunderScope = new ThunderscopeScpiConnection();
        //thunderScope.Open(Variables.Instance.ThunderScopeIp);
        //Logger.Instance.Log(LogLevel.Debug, "SCPI connection to ThunderScope opened.");
        //thunderScope.WriteLine("*IDN?");
        //var thunderScopeIdn = thunderScope.ReadLine();
        //Logger.Instance.Log(LogLevel.Debug, $"*IDN: {thunderScopeIdn}");
        //if (!thunderScopeIdn.StartsWith("EEVengers,ThunderScope", StringComparison.OrdinalIgnoreCase))
        //    throw new ApplicationException("Incorrect response from SCPI instrument (Scope).");

        //thunderScope.WriteLine("ACQ:RATE?");
        //var rate = thunderScope.ReadLine();
        //Logger.Instance.Log(LogLevel.Debug, $"ACQ:RATE? {rate}");

        //thunderScopeData = new ThunderscopeDataConnection();
        //thunderScopeData.Open(Variables.Instance.ThunderScopeIp);
        //Logger.Instance.Log(LogLevel.Debug, "Data connection to ThunderScope opened.");

        //thunderScope.WriteLine("STOP");
        //thunderScope.WriteLine("NORMAL");
        //thunderScope.WriteLine("TRIG:SOURCE NONE");
        //thunderScope.WriteLine("TRIG:TYPE EDGE");
        //thunderScope.WriteLine("TRIG:DELAY 500000000");   // Halfway through capture
        //thunderScope.WriteLine("TRIG:HOLD 0");
        //thunderScope.WriteLine("TRIG:INTER 1");
        //thunderScope.WriteLine("DEPTH 1000000");

        //thunderScope.WriteLine("CAL:FRONTEND CHAN1 DC 50 0 2786 167 0 1 20M");
        //thunderScope.WriteLine("CAL:FRONTEND CHAN2 DC 50 0 2786 167 0 1 20M");
        //thunderScope.WriteLine("CAL:FRONTEND CHAN3 DC 50 0 2786 167 0 1 20M");
        //thunderScope.WriteLine("CAL:FRONTEND CHAN4 DC 50 0 2786 167 0 1 20M");

        //thunderScope.WriteLine("RUN");

        var loggerFactory = new Microsoft.Extensions.Logging.LoggerFactory();
        var hardwareConfig = new ThunderscopeHardwareConfig
        {
            AdcChannelMode = AdcChannelMode.Single,
            SampleRateHz = 1_000_000_000,
            Resolution = AdcResolution.EightBit,
            EnabledChannels = 0x01
        };
        hardwareConfig.Frontend[0] = ThunderscopeChannelFrontend.Default();
        hardwareConfig.Frontend[1] = ThunderscopeChannelFrontend.Default();
        hardwareConfig.Frontend[2] = ThunderscopeChannelFrontend.Default();
        hardwareConfig.Frontend[3] = ThunderscopeChannelFrontend.Default();
        hardwareConfig.Calibration[0] = ThunderscopeChannelCalibrationSettings.Default().ToDriver();
        hardwareConfig.Calibration[1] = ThunderscopeChannelCalibrationSettings.Default().ToDriver();
        hardwareConfig.Calibration[2] = ThunderscopeChannelCalibrationSettings.Default().ToDriver();
        hardwareConfig.Calibration[3] = ThunderscopeChannelCalibrationSettings.Default().ToDriver();
        hardwareConfig.AdcCalibration = ThunderscopeAdcCalibrationSettings.Default().ToDriver();
        thunderScope = new Driver.Libtslitex.Thunderscope(loggerFactory, 1024 * 1024);
        thunderScope.Open(0);
        thunderScope.Configure(hardwareConfig, "");
        // Need to set manual control on all channels so that SetRate doesn't run normal logic
        var manualControl = new ThunderscopeChannelFrontendManualControl() { Coupling = ThunderscopeCoupling.DC, Termination = ThunderscopeTermination.OneMegaohm, Attenuator = 0, DAC = 2000, DPOT = 50, PgaLadderAttenuation = 0, PgaFilter = ThunderscopeBandwidth.Bw20M, PgaHighGain = 0 };
        thunderScope.SetChannelManualControl(0, manualControl);
        thunderScope.SetChannelManualControl(1, manualControl);
        thunderScope.SetChannelManualControl(2, manualControl);
        thunderScope.SetChannelManualControl(3, manualControl);
        // Start to keep the device hot
        thunderScope.Start();
        memoryRegion = new ThunderscopeMemoryRegion(1, 64 * 1024 * 1024);
        shuffleRegion = new ThunderscopeMemoryRegion(1, 64 * 1024 * 1024);
    }

    public void InitialiseSigGens(string? sigGen1Host, string? sigGen2Host)
    {
        if (string.IsNullOrWhiteSpace(sigGen1Host))
            throw new ArgumentException();
        if (string.IsNullOrWhiteSpace(sigGen2Host))
            throw new ArgumentException();

        sigGen1 = new TcpScpiConnection();
        sigGen1.Open(sigGen1Host, 5025);
        Logger.Instance.Log(LogLevel.Debug, "SCPI connection to SDG2042X #1 opened.");
        sigGen1.WriteLine("*IDN?");
        var sigGen1Idn = sigGen1.ReadLine();
        Logger.Instance.Log(LogLevel.Debug, $"*IDN: {sigGen1Idn}");
        if (!sigGen1Idn.StartsWith("Siglent Technologies,SDG2", StringComparison.OrdinalIgnoreCase))
            throw new ApplicationException("Incorrect response from *IDN?");

        sigGen1.WriteLine("C1:OUTP OFF"); Thread.Sleep(50);
        sigGen1.WriteLine("C1:OUTP LOAD, HZ"); Thread.Sleep(50);
        sigGen1.WriteLine("C1:OUTP PLRT, NOR"); Thread.Sleep(50);
        sigGen1.WriteLine("C1:BSWV WVTP, DC"); Thread.Sleep(50);
        sigGen1.WriteLine("C1:BSWV OFST, 0"); Thread.Sleep(50);
        sigGen1.WriteLine("C1:BSWV AMP, 0"); Thread.Sleep(50);

        sigGen1.WriteLine("C2:OUTP OFF"); Thread.Sleep(50);
        sigGen1.WriteLine("C2:OUTP LOAD, HZ"); Thread.Sleep(50);
        sigGen1.WriteLine("C2:OUTP PLRT, NOR"); Thread.Sleep(50);
        sigGen1.WriteLine("C2:BSWV WVTP, DC"); Thread.Sleep(50);
        sigGen1.WriteLine("C2:BSWV OFST, 0"); Thread.Sleep(50);
        sigGen1.WriteLine("C2:BSWV AMP, 0"); Thread.Sleep(50);

        sigGen2 = new TcpScpiConnection();
        sigGen2.Open(sigGen2Host, 5025);
        Logger.Instance.Log(LogLevel.Debug, "SCPI connection to SDG2042X #2 opened.");
        sigGen2.WriteLine("*IDN?");
        var sigGen2Idn = sigGen2.ReadLine();
        Logger.Instance.Log(LogLevel.Debug, $"*IDN: {sigGen2Idn}");
        if (!sigGen2Idn.StartsWith("Siglent Technologies,SDG2", StringComparison.OrdinalIgnoreCase))
            throw new ApplicationException("Incorrect response from *IDN?");

        sigGen2.WriteLine("C1:OUTP OFF"); Thread.Sleep(50);
        sigGen2.WriteLine("C1:OUTP LOAD, HZ"); Thread.Sleep(50);
        sigGen2.WriteLine("C1:OUTP PLRT, NOR"); Thread.Sleep(50);
        sigGen2.WriteLine("C1:BSWV WVTP, DC"); Thread.Sleep(50);
        sigGen2.WriteLine("C1:BSWV OFST, 0"); Thread.Sleep(50);
        sigGen2.WriteLine("C1:BSWV AMP, 0"); Thread.Sleep(50);

        sigGen2.WriteLine("C2:OUTP OFF"); Thread.Sleep(50);
        sigGen2.WriteLine("C2:OUTP LOAD, HZ"); Thread.Sleep(50);
        sigGen2.WriteLine("C2:OUTP PLRT, NOR"); Thread.Sleep(50);
        sigGen2.WriteLine("C2:BSWV WVTP, DC"); Thread.Sleep(50);
        sigGen2.WriteLine("C2:BSWV OFST, 0"); Thread.Sleep(50);
        sigGen2.WriteLine("C2:BSWV AMP, 0"); Thread.Sleep(50);
    }

    public void Close()
    {
        thunderScope?.Close();
        thunderScopeData?.Close();
        sigGen1?.Close();
        sigGen2?.Close();
    }

    public bool TryReadUserCalibration(out ThunderscopeCalibrationSettings? calibration)
    {
        if (thunderScope == null)
        {
            var loggerFactory = new Microsoft.Extensions.Logging.LoggerFactory();
            var instance = new Driver.Libtslitex.Thunderscope(loggerFactory, 1024 * 1024);
            instance.Open(0);
            var success = ThunderscopeNonVolatileMemory.TryReadUserCalibration(instance, out var calibration2);
            calibration = calibration2;
            instance.Close();
            return success;
        }
        else
        {
            var success = ThunderscopeNonVolatileMemory.TryReadUserCalibration(thunderScope, out var calibration2);
            calibration = calibration2;
            return success;
        }
    }

    public void WriteUserCalibration(ThunderscopeCalibrationSettings calibration)
    {
        if (thunderScope == null)
            throw new CalibrationException("Device must be opened");
        ThunderscopeNonVolatileMemory.WriteUserCalibration(thunderScope, calibration);
    }

    //public void SetThunderscopeChannel(int[] enabledChannelIndices, bool setDefaultRate = true)
    //{
    //    thunderScope?.WriteLine($"CHAN1:{(enabledChannelIndices.Contains(0) ? "ON" : "OFF")}");
    //    thunderScope?.WriteLine($"CHAN2:{(enabledChannelIndices.Contains(1) ? "ON" : "OFF")}");
    //    thunderScope?.WriteLine($"CHAN3:{(enabledChannelIndices.Contains(2) ? "ON" : "OFF")}");
    //    thunderScope?.WriteLine($"CHAN4:{(enabledChannelIndices.Contains(3) ? "ON" : "OFF")}");
    //    if (setDefaultRate)
    //    {
    //        // Set a default rate so that sequences get a consistent behaviour
    //        switch (enabledChannelIndices.Length)
    //        {
    //            case 1:
    //                SetThunderscopeRate(1_000_000_000);
    //                break;
    //            case 2:
    //                SetThunderscopeRate(500_000_000);
    //                break;
    //            case 3:
    //                SetThunderscopeRate(250_000_000);
    //                break;
    //            case 4:
    //                SetThunderscopeRate(250_000_000);
    //                break;
    //            default:
    //                throw new NotImplementedException();
    //        }
    //    }
    //}
    public void SetThunderscopeChannel(int[] enabledChannelIndices, bool setDefaultRate = true)
    {
        thunderScope?.SetChannelEnable(0, enabledChannelIndices.Contains(0));
        thunderScope?.SetChannelEnable(1, enabledChannelIndices.Contains(1));
        thunderScope?.SetChannelEnable(2, enabledChannelIndices.Contains(2));
        thunderScope?.SetChannelEnable(3, enabledChannelIndices.Contains(3));
        if (setDefaultRate)
        {
            switch (enabledChannelIndices.Length)
            {
                case 1:
                    thunderScope?.SetRate(1_000_000_000);
                    cachedSampleRateHz = 1_000_000_000;
                    break;
                case 2:
                    thunderScope?.SetRate(500_000_000);
                    cachedSampleRateHz = 500_000_000;
                    break;
                case 3:
                    thunderScope?.SetRate(250_000_000);
                    cachedSampleRateHz = 250_000_000;
                    break;
                case 4:
                    thunderScope?.SetRate(250_000_000);
                    cachedSampleRateHz = 250_000_000;
                    break;
                default:
                    throw new NotImplementedException();
            }

        }
    }

    //public void SetThunderscopeRate(uint rateHz)
    //{
    //    thunderScope?.WriteLine($"ACQ:RATE {rateHz}");
    //    Thread.Sleep(Variables.Instance.FrontEndSettlingTimeMs);
    //}

    public void SetThunderscopeRate(uint rateHz, CommonVariables variables)
    {
        if (rateHz != cachedSampleRateHz)
        {
            thunderScope?.SetRate(rateHz);
            cachedSampleRateHz = rateHz;
            Thread.Sleep(variables.FrontEndSettlingTimeMs);
        }
    }

    //public void SetThunderscopeCalManual50R(int channelIndex, ushort dac, byte dpot, PgaPreampGain pgaPreampGain, byte pgaLadderAttenuation)
    //{
    //    thunderScope?.WriteLine($"CAL:FRONTEND CHAN{channelIndex + 1} DC 50 0 {dac} {dpot} {pgaLadderAttenuation} {(pgaPreampGain == PgaPreampGain.High ? "1" : "0")} 20M");
    //    Thread.Sleep(Variables.Instance.FrontEndSettlingTimeMs);
    //}
    
    public void SetThunderscopeCalManual50R(int channelIndex, ushort dac, byte dpot, PgaPreampGain pgaPreampGain, byte pgaLadderAttenuation, CommonVariables variables)
    {
        SetThunderscopeCalManual50R(channelIndex, false, dac, dpot, pgaPreampGain, pgaLadderAttenuation, ThunderscopeBandwidth.Bw20M, variables);
    }

    public void SetThunderscopeCalManual50R(int channelIndex, bool attenuator, ushort dac, byte dpot, PgaPreampGain pgaPreampGain, byte pgaLadderAttenuation, ThunderscopeBandwidth pgaFilter, CommonVariables variables)
    {
        var frontend = new ThunderscopeChannelFrontendManualControl
        {
            Coupling = ThunderscopeCoupling.DC,
            Termination = ThunderscopeTermination.FiftyOhm,
            Attenuator = attenuator ? (byte)1 : (byte)0,
            DAC = dac,
            DPOT = dpot,
            PgaLadderAttenuation = pgaLadderAttenuation,
            PgaFilter = pgaFilter,
            PgaHighGain = (pgaPreampGain == PgaPreampGain.High) ? (byte)1 : (byte)0
        };
        thunderScope?.SetChannelManualControl(channelIndex, frontend);
        Thread.Sleep(variables.FrontEndSettlingTimeMs);
    }

    public void SetThunderscopeCalManual1M(int channelIndex, ushort dac, byte dpot, PgaPreampGain pgaPreampGain, byte pgaLadderAttenuation, CommonVariables variables)
    {
        SetThunderscopeCalManual1M(channelIndex, false, dac, dpot, pgaPreampGain, pgaLadderAttenuation, ThunderscopeBandwidth.Bw20M, variables);
    }

    public void SetThunderscopeCalManual1M(int channelIndex, bool attenuator, ushort dac, byte dpot, PgaPreampGain pgaPreampGain, byte pgaLadderAttenuation, ThunderscopeBandwidth pgaFilter, CommonVariables variables)
    {
        var frontend = new ThunderscopeChannelFrontendManualControl
        {
            Coupling = ThunderscopeCoupling.DC,
            Termination = ThunderscopeTermination.OneMegaohm,
            Attenuator = attenuator ? (byte)1 : (byte)0,
            DAC = dac,
            DPOT = dpot,
            PgaLadderAttenuation = pgaLadderAttenuation,
            PgaFilter = pgaFilter,
            PgaHighGain = (pgaPreampGain == PgaPreampGain.High) ? (byte)1 : (byte)0
        };
        thunderScope?.SetChannelManualControl(channelIndex, frontend);
        Thread.Sleep(variables.FrontEndSettlingTimeMs);
    }

    public void SetThunderscopeAdcCalibration(byte[] branchFineGains)
    {
        thunderScope?.SetAdcCalibration(new ThunderscopeAdcCalibration()
        {
            FineGainBranch1 = branchFineGains[0],
            FineGainBranch2 = branchFineGains[1],
            FineGainBranch3 = branchFineGains[2],
            FineGainBranch4 = branchFineGains[3],
            FineGainBranch5 = branchFineGains[4],
            FineGainBranch6 = branchFineGains[5],
            FineGainBranch7 = branchFineGains[6],
            FineGainBranch8 = branchFineGains[7]
        });
    }

    //public void GetThunderscopeStats(int channelIndex, out double average, out double min, out double max)
    //{
    //    thunderScope!.WriteLine("FORCE");
    //    var tsDataBuffer = ArrayPool<byte>.Shared.Rent(2_000_000);
    //    thunderScopeData!.RequestWaveform();
    //    var waveformHeader = thunderScopeData!.ReadWaveformHeader(tsDataBuffer);

    //    bool channelFound = false;
    //    average = 0;
    //    min = int.MaxValue;
    //    max = int.MinValue;
    //    for (int i = 0; i < waveformHeader.NumChannels; i++)
    //    {
    //        var channelHeader = thunderScopeData.ReadChannelHeader(tsDataBuffer);
    //        var channelData = thunderScopeData.ReadChannelData<sbyte>(tsDataBuffer, channelHeader);
    //        if (channelHeader.ChannelIndex != channelIndex)
    //            continue;
    //        channelFound = true;
    //        int sum = 0;
    //        foreach (var point in channelData)
    //        {
    //            sum += point;
    //            if (point < min) min = point;
    //            if (point > max) max = point;
    //        }
    //        average = (double)sum / channelData.Length;
    //        ArrayPool<byte>.Shared.Return(tsDataBuffer);
    //    }

    //    if (!channelFound)
    //        throw new CalibrationException("Channel was not in waveform data");
    //}

    public double GetThunderscopeAverage(int channelIndex)
    {
        // To do: check if channelIndex is one of the enabled channels
        var config = thunderScope!.GetConfiguration();
        thunderScope!.Stop();
        thunderScope!.Start();
        thunderScope!.Read(memoryRegion!.GetSegment(0), new CancellationToken());

        var samples = memoryRegion!.GetSegment(0).DataSpanI8;
        Span<sbyte> channel;
        switch (config.EnabledChannelsCount())
        {
            case 1:
                channel = samples;
                break;
            case 2:
                {
                    Span<sbyte> twoChannels = shuffleRegion!.GetSegment(0).DataSpanI8;
                    ShuffleI8.TwoChannels(samples, twoChannels);
                    var index = config.GetCaptureBufferIndexForTriggerChannel((TriggerChannel)(channelIndex + 1));
                    var length = samples.Length / 2;
                    channel = twoChannels.Slice(index * length, length);
                    break;
                }
            case 3:
            case 4:
                {
                    Span<sbyte> fourChannels = shuffleRegion!.GetSegment(0).DataSpanI8;
                    ShuffleI8.FourChannels(samples, fourChannels);
                    var index = config.GetCaptureBufferIndexForTriggerChannel((TriggerChannel)(channelIndex + 1));
                    var length = samples.Length / 4;
                    channel = fourChannels.Slice(index * length, length);
                    break;
                }
            default:
                throw new NotImplementedException();
        }

        long sum = 0;
        long count = 0;
        foreach (var point in channel)
        {
            sum += point;
        }
        count += channel.Length;

        return (double)sum / count;
    }

    public void GetThunderscopeFineBranches(out double[] mean, out double[] stdev)
    {
        thunderScope!.Stop();
        thunderScope!.Start();
        thunderScope!.Read(memoryRegion!.GetSegment(0), new CancellationToken());

        var sampleBuffer = memoryRegion!.GetSegment(0).DataSpanI8;
        int sampleLen = sampleBuffer.Length;

        // If multiple channels are enabled, branch parsing won't be valid
        var config = thunderScope!.GetConfiguration();
        if (config.EnabledChannelsCount() != 1)
            throw new CalibrationException("Fine branch analysis requires exactly one enabled channel.");

        int sampleCount = sampleLen / 8;

        // Branch order from HMCAD1520 datasheet, Table 27
        var branch0 = new sbyte[sampleCount]; // D1A
        var branch1 = new sbyte[sampleCount]; // D2A
        var branch2 = new sbyte[sampleCount]; // D3B
        var branch3 = new sbyte[sampleCount]; // D4B
        var branch4 = new sbyte[sampleCount]; // D2B
        var branch5 = new sbyte[sampleCount]; // D1B
        var branch6 = new sbyte[sampleCount]; // D4A
        var branch7 = new sbyte[sampleCount]; // D3A

        long sumAll = 0;
        sbyte minAll = 127;
        sbyte maxAll = -128;

        int idx = 0;
        for (int group = 0; group < sampleCount; group++)
        {
            sbyte v0 = sampleBuffer[idx++]; branch0[group] = v0; sumAll += v0; if (v0 > maxAll) maxAll = v0; if (v0 < minAll) minAll = v0; // D1A
            sbyte v1 = sampleBuffer[idx++]; branch5[group] = v1; sumAll += v1; if (v1 > maxAll) maxAll = v1; if (v1 < minAll) minAll = v1; // D1B
            sbyte v2 = sampleBuffer[idx++]; branch1[group] = v2; sumAll += v2; if (v2 > maxAll) maxAll = v2; if (v2 < minAll) minAll = v2; // D2A
            sbyte v3 = sampleBuffer[idx++]; branch4[group] = v3; sumAll += v3; if (v3 > maxAll) maxAll = v3; if (v3 < minAll) minAll = v3; // D2B
            sbyte v4 = sampleBuffer[idx++]; branch7[group] = v4; sumAll += v4; if (v4 > maxAll) maxAll = v4; if (v4 < minAll) minAll = v4; // D3A
            sbyte v5 = sampleBuffer[idx++]; branch2[group] = v5; sumAll += v5; if (v5 > maxAll) maxAll = v5; if (v5 < minAll) minAll = v5; // D3B
            sbyte v6 = sampleBuffer[idx++]; branch6[group] = v6; sumAll += v6; if (v6 > maxAll) maxAll = v6; if (v6 < minAll) minAll = v6; // D4A
            sbyte v7 = sampleBuffer[idx++]; branch3[group] = v7; sumAll += v7; if (v7 > maxAll) maxAll = v7; if (v7 < minAll) minAll = v7; // D4B
        }

        double meanAll = (double)sumAll / (double)(sampleCount * 8);

        static double CalcMean(sbyte[] data)
        {
            if (data.Length == 0) return 0d;
            long s = 0;
            for (int i = 0; i < data.Length; i++) s += data[i];
            return (double)s / data.Length;
        }
        static double CalcSampleStdDev(sbyte[] data, double mean)
        {
            if (data.Length <= 1) return 0d;
            double ssd = 0d;
            for (int i = 0; i < data.Length; i++)
            {
                double d = data[i] - mean;
                ssd += d * d;
            }
            return Math.Sqrt(ssd / (data.Length - 1));
        }

        var branchMeans = new double[8];
        var branchStdDevs = new double[8];
        branchMeans[0] = CalcMean(branch0); branchStdDevs[0] = CalcSampleStdDev(branch0, branchMeans[0]);
        branchMeans[1] = CalcMean(branch1); branchStdDevs[1] = CalcSampleStdDev(branch1, branchMeans[1]);
        branchMeans[2] = CalcMean(branch2); branchStdDevs[2] = CalcSampleStdDev(branch2, branchMeans[2]);
        branchMeans[3] = CalcMean(branch3); branchStdDevs[3] = CalcSampleStdDev(branch3, branchMeans[3]);
        branchMeans[4] = CalcMean(branch4); branchStdDevs[4] = CalcSampleStdDev(branch4, branchMeans[4]);
        branchMeans[5] = CalcMean(branch5); branchStdDevs[5] = CalcSampleStdDev(branch5, branchMeans[5]);
        branchMeans[6] = CalcMean(branch6); branchStdDevs[6] = CalcSampleStdDev(branch6, branchMeans[6]);
        branchMeans[7] = CalcMean(branch7); branchStdDevs[7] = CalcSampleStdDev(branch7, branchMeans[7]);

        //DebugLog.Instance.Log($"ADC sample mean: {meanAll:F1}, min: {minAll}, max: {maxAll}");
        //for (int i = 0; i < 8; i++)
        //    DebugLog.Instance.Log($"Branch {i}: mean={branchMeans[i]:F3}, stddev={branchStdDevs[i]:F3}");

        mean = branchMeans;
        stdev = branchStdDevs;
    }

    /// <summary>
    /// This is the same as AC RMS
    /// </summary>
    public double GetThunderscopePopulationStdDev(int channelIndex)
    {
        // To do: check if channelIndex is one of the enabled channels
        var config = thunderScope!.GetConfiguration();
        thunderScope!.Stop();
        thunderScope!.Start();
        thunderScope!.Read(memoryRegion!.GetSegment(0), new CancellationToken());

        var samples = memoryRegion!.GetSegment(0).DataSpanI8;
        Span<sbyte> channel;
        switch (config.EnabledChannelsCount())
        {
            case 1:
                channel = samples;
                break;
            case 2:
                {
                    Span<sbyte> twoChannels = shuffleRegion!.GetSegment(0).DataSpanI8;
                    ShuffleI8.TwoChannels(samples, twoChannels);
                    var index = config.GetCaptureBufferIndexForTriggerChannel((TriggerChannel)(channelIndex + 1));
                    var length = samples.Length / 2;
                    channel = twoChannels.Slice(index * length, length);
                    break;
                }
            case 3:
            case 4:
                {
                    Span<sbyte> fourChannels = shuffleRegion!.GetSegment(0).DataSpanI8;
                    ShuffleI8.FourChannels(samples, fourChannels);
                    var index = config.GetCaptureBufferIndexForTriggerChannel((TriggerChannel)(channelIndex + 1));
                    var length = samples.Length / 4;
                    channel = fourChannels.Slice(index * length, length);
                    break;
                }
            default:
                throw new NotImplementedException();
        }

        double sum = 0;
        foreach (var point in channel)
        {
            sum += point;
        }

        double mean = sum / channel.Length;

        double sumSquares = 0;
        foreach (var point in channel)
        {
            sumSquares += Math.Pow(point - mean, 2);
        }

        return Math.Sqrt(sumSquares / channel.Length);
    }

    public void SetSdgChannel(int channelIndex)
    {
        switch (channelIndex)
        {
            case -1:
                sigGen1?.WriteLine($"C1:OUTP OFF"); Thread.Sleep(200);
                sigGen1?.WriteLine($"C2:OUTP OFF"); Thread.Sleep(200);
                sigGen2?.WriteLine($"C1:OUTP OFF"); Thread.Sleep(200);
                sigGen2?.WriteLine($"C2:OUTP OFF"); Thread.Sleep(200);
                break;
            case 0:
                sigGen1?.WriteLine($"C1:OUTP ON"); Thread.Sleep(200);
                sigGen1?.WriteLine($"C2:OUTP OFF"); Thread.Sleep(200);
                sigGen2?.WriteLine($"C1:OUTP OFF"); Thread.Sleep(200);
                sigGen2?.WriteLine($"C2:OUTP OFF"); Thread.Sleep(200);
                break;
            case 1:
                sigGen1?.WriteLine($"C1:OUTP OFF"); Thread.Sleep(200);
                sigGen1?.WriteLine($"C2:OUTP ON"); Thread.Sleep(200);
                sigGen2?.WriteLine($"C1:OUTP OFF"); Thread.Sleep(200);
                sigGen2?.WriteLine($"C2:OUTP OFF"); Thread.Sleep(200);
                break;
            case 2:
                sigGen1?.WriteLine($"C1:OUTP OFF"); Thread.Sleep(200);
                sigGen1?.WriteLine($"C2:OUTP OFF"); Thread.Sleep(200);
                sigGen2?.WriteLine($"C1:OUTP ON"); Thread.Sleep(200);
                sigGen2?.WriteLine($"C2:OUTP OFF"); Thread.Sleep(200);
                break;
            case 3:
                sigGen1?.WriteLine($"C1:OUTP OFF"); Thread.Sleep(200);
                sigGen1?.WriteLine($"C2:OUTP OFF"); Thread.Sleep(200);
                sigGen2?.WriteLine($"C1:OUTP OFF"); Thread.Sleep(200);
                sigGen2?.WriteLine($"C2:OUTP ON"); Thread.Sleep(200);
                break;
        }
    }

    public void SetSdgOffset(int channelIndex, double voltage)
    {
        GetSdgReference(channelIndex, out var sigGen, out var sdgChannel);
        sigGen.WriteLine($"{sdgChannel}:BSWV OFST, {voltage:F4}"); Thread.Sleep(50);
    }

    public void SetSdgDc(int channelIndex)
    {
        GetSdgReference(channelIndex, out var sigGen, out var sdgChannel);
        sigGen.WriteLine($"{sdgChannel}:BSWV WVTP, DC"); Thread.Sleep(50);
    }

    public void SetSdgSine(int channelIndex, double vpp, uint freqHz)
    {
        GetSdgReference(channelIndex, out var sigGen, out var sdgChannel);
        sigGen.WriteLine($"{sdgChannel}:BSWV WVTP, SINE"); Thread.Sleep(50);
        sigGen.WriteLine($"{sdgChannel}:BSWV FRQ, {freqHz}"); Thread.Sleep(50);
        sigGen.WriteLine($"{sdgChannel}:BSWV AMP, {vpp}"); Thread.Sleep(50);
        sigGen.WriteLine($"{sdgChannel}:BSWV OFST, 0"); Thread.Sleep(50);
    }

    public void SetSdgNoise(int channelIndex, double stdev, double mean)
    {
        GetSdgReference(channelIndex, out var sigGen, out var sdgChannel);
        sigGen.WriteLine($"{sdgChannel}:BSWV WVTP, NOISE"); Thread.Sleep(50);
        sigGen.WriteLine($"{sdgChannel}:BSWV STDEV, {stdev}"); Thread.Sleep(50);
        sigGen.WriteLine($"{sdgChannel}:BSWV MEAN, {mean}"); Thread.Sleep(50);
    }

    public void SetSdgFrequency(int channelIndex, uint frequencyHz)
    {
        GetSdgReference(channelIndex, out var sigGen, out var sdgChannel);
        sigGen.WriteLine($"{sdgChannel}:BSWV FRQ, {frequencyHz}"); Thread.Sleep(50);
    }

    private void GetSdgReference(int channelIndex, out TcpScpiConnection sigGen, out string sdgChannel)
    {
        if (sigGen1 == null || sigGen2 == null)
            throw new NullReferenceException();

        switch (channelIndex)
        {
            case 0:
                sigGen = sigGen1;
                sdgChannel = "C1";
                return;
            case 1:
                sigGen = sigGen1;
                sdgChannel = "C2";
                return;
            case 2:
                sigGen = sigGen2;
                sdgChannel = "C1";
                return;
            case 3:
                sigGen = sigGen2;
                sdgChannel = "C2";
                return;
            default:
                throw new NotImplementedException();
        }
    }
}
