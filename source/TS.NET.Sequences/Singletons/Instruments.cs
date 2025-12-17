using CommunityToolkit.HighPerformance.Buffers;

namespace TS.NET.Sequences;

public class Instruments
{
    private static readonly Lazy<Instruments> lazy = new(() => new Instruments());
    public static Instruments Instance { get { return lazy.Value; } }
    private Instruments() { }

    //private ThunderscopeScpiConnection? thunderScope;
    private Driver.Libtslitex.Thunderscope? thunderScope;
    private ThunderscopeDataConnection? thunderScopeData;

    private ThunderscopeMemory? dataMemory;
    private ThunderscopeMemory? shuffleMemory;

    private uint cachedSampleRateHz = 0;
    private AdcResolution cachedResolution = AdcResolution.EightBit;
    private int[] cachedChannelIndices = [];

    public void Initialise()
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
            Acquisition = new ThunderscopeAcquisitionConfig()
            {
                AdcChannelMode = AdcChannelMode.Single,
                EnabledChannels = 0x01,
                SampleRateHz = 1_000_000_000,
                Resolution = AdcResolution.EightBit
            }
        };
        cachedSampleRateHz = 1_000_000_000;
        cachedResolution = AdcResolution.EightBit;
        cachedChannelIndices = [0];

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
        dataMemory = new ThunderscopeMemory(512 * 1024 * 1024);
        shuffleMemory = new ThunderscopeMemory(512 * 1024 * 1024);
    }

    public void Close()
    {
        thunderScope?.Close();
        thunderScopeData?.Close();
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
            throw new TestbenchException("Device must be opened");
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
    public void SetThunderscopeChannel(int[] enabledChannelIndices)
    {
        if (!enabledChannelIndices.SequenceEqual(cachedChannelIndices))
        {
            thunderScope?.SetChannelEnable(0, enabledChannelIndices.Contains(0));
            thunderScope?.SetChannelEnable(1, enabledChannelIndices.Contains(1));
            thunderScope?.SetChannelEnable(2, enabledChannelIndices.Contains(2));
            thunderScope?.SetChannelEnable(3, enabledChannelIndices.Contains(3));
            cachedSampleRateHz = 0;
            cachedChannelIndices = enabledChannelIndices;
        }
    }

    //public void SetThunderscopeRate(uint rateHz)
    //{
    //    thunderScope?.WriteLine($"ACQ:RATE {rateHz}");
    //    Thread.Sleep(Variables.Instance.FrontEndSettlingTimeMs);
    //}

    public void SetThunderscopeRate(uint rateHz)
    {
        if (rateHz != cachedSampleRateHz)
        {
            thunderScope?.SetRate(rateHz);
            cachedSampleRateHz = rateHz;
        }
    }

    public void SetThunderscopeResolution(AdcResolution resolution)
    {
        if (resolution != cachedResolution)
        {
            thunderScope?.SetResolution(resolution);
            cachedResolution = resolution;
        }
    }

    public void SetThunderscopeFrontend(int channelIndex, ThunderscopeChannelFrontendManualControl frontend, int frontEndSettlingTimeMs)
    {
        thunderScope?.SetChannelManualControl(channelIndex, frontend);
        Thread.Sleep(frontEndSettlingTimeMs);
    }

    public void SetThunderscopeCalManual50R(int channelIndex, ushort dac, byte dpot, PgaPreampGain pgaPreampGain, byte pgaLadderAttenuation, int frontEndSettlingTimeMs)
    {
        SetThunderscopeCalManual50R(channelIndex, false, dac, dpot, pgaPreampGain, pgaLadderAttenuation, ThunderscopeBandwidth.Bw20M, frontEndSettlingTimeMs);
    }

    public void SetThunderscopeCalManual50R(int channelIndex, bool attenuator, ushort dac, byte dpot, PgaPreampGain pgaPreampGain, byte pgaLadderAttenuation, ThunderscopeBandwidth pgaFilter, int frontEndSettlingTimeMs)
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
        Thread.Sleep(frontEndSettlingTimeMs);
    }

    public void SetThunderscopeCalManual1M(int channelIndex, ushort dac, byte dpot, PgaPreampGain pgaPreampGain, byte pgaLadderAttenuation, int frontEndSettlingTimeMs)
    {
        SetThunderscopeCalManual1M(channelIndex, false, dac, dpot, pgaPreampGain, pgaLadderAttenuation, ThunderscopeBandwidth.Bw20M, frontEndSettlingTimeMs);
    }

    public void SetThunderscopeCalManual1M(int channelIndex, bool attenuator, ushort dac, byte dpot, PgaPreampGain pgaPreampGain, byte pgaLadderAttenuation, ThunderscopeBandwidth pgaFilter, int frontEndSettlingTimeMs)
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
        Thread.Sleep(frontEndSettlingTimeMs);
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

    public void SetThunderscopeAdcCalibration(ThunderscopeAdcCalibration adcCalibration)
    {
        thunderScope?.SetAdcCalibration(adcCalibration);
    }

    public double GetThunderscopeFpgaTemp()
    {
        return thunderScope!.GetStatus().FpgaTemp;
    }

    public double GetThunderscopeAverage(int channelIndex, int sampleCount)
    {
        var config = thunderScope!.GetConfiguration();
        if (!config.Acquisition.IsChannelIndexAnEnabledChannel(channelIndex))
            throw new TestbenchException("Requested channel index is not an enabled channel");

        using SpanOwner<sbyte> i8Buffer = SpanOwner<sbyte>.Allocate(sampleCount);           // Returned to pool when it goes out of scope
        GetChannelDataI8(channelIndex, sampleCount, i8Buffer.Span);

        long sum = 0;
        long count = 0;
        int min = int.MaxValue;
        int max = int.MinValue;
        foreach (var point in i8Buffer.Span)
        {
            sum += point;
            // Temporary debug min/max
            if (point > max)
                max = point;
            if (point < min)
                min = point;
        }
        count += i8Buffer.Span.Length;
        var average = (double)sum / count;

        return average;
    }

    public void GetThunderscopeFineBranches(out double[] mean, out double[] stdev)
    {
        thunderScope!.Stop();
        thunderScope!.Start();
        var subsetDataMemory = dataMemory!.Subset(32 * 1024 * 1024);
        thunderScope!.Read(subsetDataMemory);

        var sampleBuffer = subsetDataMemory.DataSpanI8;
        int sampleLen = sampleBuffer.Length;

        // If multiple channels are enabled, branch parsing won't be valid
        var config = thunderScope!.GetConfiguration();
        if (config.Acquisition.EnabledChannelsCount() != 1)
            throw new TestbenchException("Fine branch analysis requires exactly one enabled channel.");

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
    
    public void GetThunderscopeFineBranchesSine(double frequency, double sampleRateHz, double inputVpp, AdcResolution resolution, out double[] amplitudes, out double[] phases, out double[] offsets)
    {
        // Note: phases are not reliable, too much phase noise in the test system.
        // Approximate magnitudes of errors are:
        //    amplitude: 4 LSB
        //    offset: 0.2 LSB
        
        // If multiple channels are enabled, branch parsing won't be valid
        var config = thunderScope!.GetConfiguration();
        if (config.Acquisition.EnabledChannelsCount() != 1)
            throw new TestbenchException("Fine branch analysis requires exactly one enabled channel.");

        // Aim to get 20 cycles of the sig-gen waveform in each of the 8 branch buffers
        var cycleCount = 20.0 * 8.0;
        var totalSampleCount = (int)Math.Round((sampleRateHz / frequency) * cycleCount);

        thunderScope!.Stop();
        thunderScope!.Start();
        var subsetDataMemory = dataMemory!.Subset(32 * 1024 * 1024);
        thunderScope!.Read(subsetDataMemory);

        var sampleBuffer = subsetDataMemory.DataSpanI8.Slice(subsetDataMemory.DataSpanI8.Length - totalSampleCount, totalSampleCount);
        int sampleCount = sampleBuffer.Length / 8;

        // Branch order from HMCAD1520 datasheet, Table 27
        //                               LVDS output [branch]
        var branch1 = new sbyte[sampleCount]; // D1A [1]
        var branch2 = new sbyte[sampleCount]; // D2A [2]
        var branch3 = new sbyte[sampleCount]; // D3B [3]
        var branch4 = new sbyte[sampleCount]; // D4B [4]
        var branch5 = new sbyte[sampleCount]; // D2B [5]
        var branch6 = new sbyte[sampleCount]; // D1B [6]
        var branch7 = new sbyte[sampleCount]; // D4A [7]
        var branch8 = new sbyte[sampleCount]; // D3A [8]

        int idx = 0;
        for (int group = 0; group < sampleCount; group++)
        {
            branch1[group] = sampleBuffer[idx++]; // D1A
            branch6[group] = sampleBuffer[idx++]; // D1B
            branch2[group] = sampleBuffer[idx++]; // D2A
            branch5[group] = sampleBuffer[idx++]; // D2B
            branch8[group] = sampleBuffer[idx++]; // D3A
            branch3[group] = sampleBuffer[idx++]; // D3B
            branch7[group] = sampleBuffer[idx++]; // D4A
            branch4[group] = sampleBuffer[idx++]; // D4B
        }
        // Do LSQ on sine waves
        amplitudes = new double[8];
        phases = new double[8];
        offsets = new double[8];
        for (int branch = 0; branch < 8; branch++)
        {
            using SpanOwner<double> f64Buffer = SpanOwner<double>.Allocate(sampleCount);  // Returned to pool when it goes out of scope
            var i8Span = branch switch
            {
                0 => branch1,
                1 => branch2,
                2 => branch3,
                3 => branch4,
                4 => branch5,
                5 => branch6,
                6 => branch7,
                7 => branch8,
                _ => throw new NotImplementedException()
            };
            var f64Span = f64Buffer.Span;

            var vPerBit = resolution switch
            {
                AdcResolution.EightBit => (double)(inputVpp / 256.0),
                AdcResolution.TwelveBit => (double)(inputVpp / 4096.0),
                _ => throw new NotImplementedException()
            };

            for (int i = 0; i < f64Span.Length; i++)
            {
                f64Span[i] = i8Span[i] * vPerBit;
            }
            var (amplitude, phaseDeg, dcOffset) = SineLeastSquaresFit.FitSineWave(sampleRateHz/8, f64Span, frequency);
            var lsqVpp = amplitude * 2.0;
            amplitudes[branch] =  lsqVpp;
            phases[branch] = phaseDeg;
            offsets[branch] = dcOffset;
        }
    }

    /// <summary>
    /// This is the same as AC RMS
    /// </summary>
    public double GetThunderscopePopulationStdDev(int channelIndex, int sampleCount)
    {
        var config = thunderScope!.GetConfiguration();
        if (!config.Acquisition.IsChannelIndexAnEnabledChannel(channelIndex))
            throw new TestbenchException("Requested channel index is not an enabled channel");

        using SpanOwner<sbyte> i8Buffer = SpanOwner<sbyte>.Allocate(sampleCount);           // Returned to pool when it goes out of scope
        GetChannelDataI8(channelIndex, sampleCount, i8Buffer.Span);

        double sum = 0;
        foreach (var point in i8Buffer.Span)
        {
            sum += point;
        }

        double mean = sum / i8Buffer.Span.Length;

        double sumSquares = 0;
        foreach (var point in i8Buffer.Span)
        {
            sumSquares += Math.Pow(point - mean, 2);
        }

        return Math.Sqrt(sumSquares / i8Buffer.Span.Length);
    }

    public double GetThunderscopeVppAtFrequencyLsq(int channelIndex, double frequency, double sampleRateHz, double inputVpp, AdcResolution resolution)
    {
        var config = thunderScope!.GetConfiguration();
        if (!config.Acquisition.IsChannelIndexAnEnabledChannel(channelIndex))
            throw new TestbenchException("Requested channel index is not an enabled channel");

        var cycleCount = 10.0;
        var sampleCount = (int)Math.Round((sampleRateHz / frequency) * cycleCount);

        using SpanOwner<sbyte> i8Buffer = SpanOwner<sbyte>.Allocate(sampleCount);           // Returned to pool when it goes out of scope
        GetChannelDataI8(channelIndex, sampleCount, i8Buffer.Span);
        using SpanOwner<double> f64Buffer = SpanOwner<double>.Allocate(sampleCount);  // Returned to pool when it goes out of scope
        var i8Span = i8Buffer.Span;
        var f64Span = f64Buffer.Span;

        var vPerBit = resolution switch
        {
            AdcResolution.EightBit => (double)(inputVpp / 256.0),
            AdcResolution.TwelveBit => (double)(inputVpp / 4096.0),
            _ => throw new NotImplementedException()
        };

        for (int i = 0; i < f64Span.Length; i++)
        {
            f64Span[i] = i8Span[i] * vPerBit;
        }

        // Goertzel & LSQ give approximately same results for amplitude, Goertzel is slightly faster, LSQ needs optimisation.
        // LSQ should be more numerically stable.
        // Remember square waves will have a larger amplitude fitted to them.

        //GoertzelFilter filter = new GoertzelFilter(frequency, sampleRateHz);
        //var result = filter.Process(f64Span);
        //var goertzelVp = (2.0/f64Span.Length) * result.Magnitude;
        //var goertzelVpp = goertzelVp * 2.0;
        //double goertzelVrms = goertzelVp / Math.Sqrt(2.0);

        var (amplitude, phaseDeg, dcOffset) = SineLeastSquaresFit.FitSineWave(sampleRateHz, f64Span, frequency);
        var lsqVpp = amplitude * 2.0;

        return lsqVpp;
    }

    public void GetChannelDataI8(int channelIndex, int sampleCount, Span<sbyte> outputBuffer)
    {
        var config = thunderScope!.GetConfiguration();
        if (!config.Acquisition.IsChannelIndexAnEnabledChannel(channelIndex))
            throw new TestbenchException("Requested channel index is not an enabled channel");
        if(config.Acquisition.Resolution != AdcResolution.EightBit)
            throw new TestbenchException("Acquisition not set up up for 8 bit");
        if (outputBuffer.Length != sampleCount)
            throw new TestbenchException("Output buffer has incorrect length");

        var channelCount = config.Acquisition.EnabledChannelsCount();
        var interleavedSampleCount = channelCount switch
        {
            1 => sampleCount,
            2 => sampleCount * 2,
            3 => sampleCount * 4,
            4 => sampleCount * 4,
            _ => throw new NotImplementedException()
        };
        var minimumAcquisitionLength = 1024 * 1024;
        var acquisitionLength = ((interleavedSampleCount / minimumAcquisitionLength) + 1) * minimumAcquisitionLength;
        if (acquisitionLength < minimumAcquisitionLength)
            acquisitionLength = minimumAcquisitionLength;

        thunderScope!.Stop();
        thunderScope!.Start();
        var subsetDataMemory = dataMemory!.Subset(acquisitionLength);
        var subsetShuffleMemory = shuffleMemory!.Subset(acquisitionLength);
        thunderScope!.Read(subsetDataMemory);

        Span<sbyte> channel;
        switch (config.Acquisition.EnabledChannelsCount())
        {
            case 1:
                {
                    channel = subsetDataMemory.DataSpanI8;
                    break;
                }
            case 2:
                {
                    ShuffleI8.TwoChannels(subsetDataMemory.DataSpanI8, subsetShuffleMemory.DataSpanI8);
                    var index = config.Acquisition.GetCaptureBufferIndexForTriggerChannel((TriggerChannel)(channelIndex + 1));
                    var length = subsetDataMemory.DataSpanI8.Length / 2;
                    channel = subsetShuffleMemory.DataSpanI8.Slice(index * length, length);
                    break;
                }
            case 3:
            case 4:
                {
                    ShuffleI8.FourChannels(subsetDataMemory.DataSpanI8, subsetShuffleMemory.DataSpanI8);
                    var index = config.Acquisition.GetCaptureBufferIndexForTriggerChannel((TriggerChannel)(channelIndex + 1));
                    var length = subsetDataMemory.DataSpanI8.Length / 4;
                    channel = subsetShuffleMemory.DataSpanI8.Slice(index * length, length);
                    break;
                }
            default:
                throw new NotImplementedException();
        }

        // Acquisition buffer is likely larger than requested sampleCount so use the end of the acquisition buffer and copy to output buffer
        channel = channel.Slice(channel.Length - sampleCount, sampleCount);
        channel.CopyTo(outputBuffer);
    }

    //public (double amplitude, double phaseDeg, double offset) GetThunderscopeBodeAtFrequencyLsq(int channelIndex, double frequency, double sampleRateHz, double inputVpp, AdcResolution resolution)
    //{
    //    var config = thunderScope!.GetConfiguration();
    //    if (config.Acquisition.EnabledChannelsCount() != 2)
    //        throw new TestbenchException("2 channels must be enabled");
    //    if (!config.Acquisition.IsChannelIndexAnEnabledChannel(channelIndex))
    //        throw new TestbenchException("Requested channel index is not an enabled channel");

    //    int triggerChannelIndex = channelIndex switch
    //    {
    //        0 => 1,
    //        1 => 0,
    //        _ => throw new NotImplementedException()
    //    };

    //    thunderScope!.Stop();
    //    thunderScope!.Start();
    //    var subsetDataMemory = dataMemory!.Subset(32 * 1024 * 1024);
    //    var subsetShuffleMemory = shuffleMemory!.Subset(32 * 1024 * 1024);
    //    thunderScope!.Read(subsetDataMemory);

    //    var sampleCount = (int)((sampleRateHz / frequency) * 10.0);
    //    var samples = subsetDataMemory.DataSpanI8;//.Slice(subsetDataMemory.DataSpanI8.Length - sampleCount, sampleCount);
    //    Span<sbyte> triggerChannel;
    //    Span<sbyte> dataChannel;
    //    switch (config.Acquisition.EnabledChannelsCount())
    //    {
    //        case 2:
    //            {
    //                Span<sbyte> twoChannels = subsetShuffleMemory.DataSpanI8;
    //                ShuffleI8.TwoChannels(samples, twoChannels);
    //                var length = samples.Length / 2;
    //                dataChannel = twoChannels.Slice(channelIndex * length, length);
    //                triggerChannel = twoChannels.Slice(triggerChannelIndex * length, length);
    //                break;
    //            }
    //        default:
    //            throw new NotImplementedException();
    //    }

    //    var trigger = new RisingEdgeTriggerI8(new EdgeTriggerParameters() { Direction = EdgeDirection.Rising, HysteresisPercent = 5, LevelV = 0 }, 0.8);
    //    var triggerResults = new EdgeTriggerResults()
    //    {
    //        ArmIndices = new ulong[100],
    //        TriggerIndices = new ulong[100],
    //        CaptureEndIndices = new ulong[100]
    //    };
    //    trigger.Process(triggerChannel, 0, ref triggerResults);
    //    if (triggerResults.TriggerCount == 0)
    //        throw new TestbenchException("No triggers found");
    //    if (triggerResults.CaptureEndCount == 0)
    //        throw new TestbenchException("No captures found");

    //    var triggeredData = dataChannel.Slice((int)triggerResults.TriggerIndices[0], sampleCount);
    //    var squareData = triggerChannel.Slice((int)triggerResults.TriggerIndices[0], sampleCount);

    //    double[] scaledSamples = new double[triggeredData.Length];
    //    var vPerBit = resolution switch
    //    {
    //        AdcResolution.EightBit => (double)(inputVpp / 256.0),
    //        AdcResolution.TwelveBit => (double)(inputVpp / 4096.0),
    //        _ => throw new NotImplementedException()
    //    };

    //    for (int i = 0; i < scaledSamples.Length; i++)
    //    {
    //        scaledSamples[i] = triggeredData[i] * vPerBit;
    //    }

    //    return SineLeastSquaresFit.FitSineWave(sampleRateHz, scaledSamples, frequency);
    //}
}
