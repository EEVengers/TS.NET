using CommunityToolkit.HighPerformance.Buffers;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

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

    public string Initialise()
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

        // Load serial from first thunderscope found
        var hwidSerial = "";
        var devices = Driver.Libtslitex.Thunderscope.ListDevices();
        if (devices.Count > 0)
        {
            hwidSerial = devices[0].Serial;
        }

        var loggerFactory = new Microsoft.Extensions.Logging.LoggerFactory();
        var hardwareConfig = new ThunderscopeHardwareConfig
        {
            Acquisition = new ThunderscopeAcquisitionConfig()
            {
                AdcChannelMode = AdcChannelMode.Single,
                EnabledChannels = 0x01,
                SampleRateHz = 1_000_000_000,
                Resolution = AdcResolution.EightBit
            },
            //RefClockMode = ThunderscopeRefClockMode.Input,
            RefClockMode = ThunderscopeRefClockMode.Disabled,
            RefClockFrequencyHz = 10_000_000
        };
        cachedSampleRateHz = 1_000_000_000;
        cachedResolution = AdcResolution.EightBit;
        cachedChannelIndices = [0];

        hardwareConfig.Frontend[0] = ThunderscopeChannelFrontend.Default();
        hardwareConfig.Frontend[1] = ThunderscopeChannelFrontend.Default();
        hardwareConfig.Frontend[2] = ThunderscopeChannelFrontend.Default();
        hardwareConfig.Frontend[3] = ThunderscopeChannelFrontend.Default();
        thunderScope = new Driver.Libtslitex.Thunderscope(loggerFactory, 1024 * 1024);
        thunderScope.Open(0);
        thunderScope.Configure(hardwareConfig, Calibration.Default(), "");
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

        Thread.Sleep(100);
        var status = thunderScope.GetStatus();
        Debug.WriteLine($"RefInClk: {status.RefClockInValid}");

        return hwidSerial;
    }

    public void Close()
    {
        thunderScope?.Close();
        thunderScopeData?.Close();
    }

    public bool TryReadUserCalibration(out Calibration? calibration)
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

    public void WriteUserCalibration(Calibration calibration)
    {
        if (thunderScope == null)
            throw new TestbenchException("Device must be opened");
        ThunderscopeNonVolatileMemory.WriteUserCalibration(thunderScope, calibration);
    }

    public void SetThunderscopeChannel(int[] enabledChannelIndices)
    {
        if (!enabledChannelIndices.SequenceEqual(cachedChannelIndices))
        {
            thunderScope?.SetChannelEnable(0, enabledChannelIndices.Contains(0), updateFrontends: false);
            thunderScope?.SetChannelEnable(1, enabledChannelIndices.Contains(1), updateFrontends: false);
            thunderScope?.SetChannelEnable(2, enabledChannelIndices.Contains(2), updateFrontends: false);
            thunderScope?.SetChannelEnable(3, enabledChannelIndices.Contains(3), updateFrontends: false);
            cachedSampleRateHz = 0;
            cachedChannelIndices = enabledChannelIndices;
        }
    }

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

    public void SetThunderscopeBranchGains(byte[] branchGains)
    {
        thunderScope?.SetAdcBranchGainManualControl(branchGains);
    }

    public double GetThunderscopeFpgaTemp()
    {
        return thunderScope!.GetStatus().FpgaTemp;
    }

    public bool GetThunderscopeRefClockInValid()
    {
        return thunderScope!.GetStatus().RefClockInValid;
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
        var interleavedData = GetDataI8InterleavedExactLength(32 * 1024 * 1024);

        var sampleBuffer = interleavedData;
        int sampleLen = sampleBuffer.Length;

        // If multiple channels are enabled, branch parsing won't be valid
        var config = thunderScope!.GetConfiguration();
        if (config.Acquisition.EnabledChannelsCount() != 1)
            throw new TestbenchException("Fine branch analysis requires exactly one enabled channel.");

        var channelBranchIndices = GetBranchLayout(config.Acquisition.AdcChannelMode);

        int branchSampleCount = sampleLen / 8;

        var branches = new sbyte[8][];
        for (int i = 0; i < 8; i++)
        {
            branches[i] = new sbyte[branchSampleCount];
        }

        long sumAll = 0;
        sbyte minAll = 127;
        sbyte maxAll = -128;

        int idx = 0;
        for (int sample = 0; sample < branchSampleCount; sample++)
        {
            for (int branch = 0; branch < 8; branch++)
            {
                var value = sampleBuffer[idx++];
                branches[branch][sample] = value;

                sumAll += value;
                if (value > maxAll)
                    maxAll = value;
                if (value < minAll)
                    minAll = value;
            }
        }

        double meanAll = (double)sumAll / (double)(branchSampleCount * 8);

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
        for (int i = 0; i < 8; i++)
        {
            branchMeans[i] = CalcMean(branches[i]);
            branchStdDevs[i] = CalcSampleStdDev(branches[i], branchMeans[i]);
        }

        //DebugLog.Instance.Log($"ADC sample mean: {meanAll:F1}, min: {minAll}, max: {maxAll}");
        //for (int i = 0; i < 8; i++)
        //    DebugLog.Instance.Log($"Branch {i}: mean={branchMeans[i]:F3}, stddev={branchStdDevs[i]:F3}");

        mean = branchMeans;
        stdev = branchStdDevs;
    }

    public void GetThunderscopeFineBranchesSine(double frequency, double sampleRateHz, out double[] amplitudes, out double[] phases, out double[] offsets)
    {
        // Note: phases are not reliable, too much phase noise in the test system.
        // Approximate magnitudes of errors are:
        //    amplitude: 4 LSB
        //    offset: 0.2 LSB

        var config = thunderScope!.GetConfiguration();
        // Calculate how many samples are needed per channel to get 20 cycles of the sig-gen waveform in each branch buffer
        var branchCountPerChannel = 8 / (int)config.Acquisition.AdcChannelMode;
        var samplesPerCycle = sampleRateHz / frequency;

        var interleavedSampleCount = (int)(samplesPerCycle * 20.0 * (int)config.Acquisition.AdcChannelMode);
        var samplesPerBranch = interleavedSampleCount / 8;
        Debug.WriteLine(samplesPerBranch);
        var branches = new sbyte[8][];
        for (int i = 0; i < 8; i++)
        {
            branches[i] = new sbyte[samplesPerBranch];
        }

        var interleavedData = GetDataI8InterleavedExactLength(interleavedSampleCount);
        var channelBranchIndices = GetBranchLayout(config.Acquisition.AdcChannelMode);
        int idx = 0;
        for (int branchSample = 0; branchSample < samplesPerBranch; branchSample++)
        {
            branches[channelBranchIndices[0]][branchSample] = interleavedData[idx++];
            branches[channelBranchIndices[1]][branchSample] = interleavedData[idx++];
            branches[channelBranchIndices[2]][branchSample] = interleavedData[idx++];
            branches[channelBranchIndices[3]][branchSample] = interleavedData[idx++];
            branches[channelBranchIndices[4]][branchSample] = interleavedData[idx++];
            branches[channelBranchIndices[5]][branchSample] = interleavedData[idx++];
            branches[channelBranchIndices[6]][branchSample] = interleavedData[idx++];
            branches[channelBranchIndices[7]][branchSample] = interleavedData[idx++];
        }

        amplitudes = new double[8];
        phases = new double[8];
        offsets = new double[8];
        for (int i = 0; i < 8; i++)
        {
            var branchData = branches[i];
            using SpanOwner<double> f64Buffer = SpanOwner<double>.Allocate(samplesPerBranch);  // Returned to pool when it goes out of scope
            var f64Span = f64Buffer.Span;

            for (int sample = 0; sample < samplesPerBranch; sample++)
            {
                f64Span[sample] = branchData[sample];
            }

            var (amplitude, phaseDeg, dcOffset) = SineLeastSquaresFit.FitSineWave(sampleRateHz / branchCountPerChannel, f64Span, frequency);
            amplitudes[i] = Math.Round(amplitude * 2.0, 3);
            phases[i] = Math.Round(phaseDeg, 3);
            offsets[i] = Math.Round(dcOffset, 3);
        }
    }

    private int[] GetBranchLayout(AdcChannelMode mode)
    {
        switch (mode)
        {
            case AdcChannelMode.Single:
                return [0, 5, 1, 4, 7, 2, 6, 3];
            case AdcChannelMode.Dual:
                return [0, 4, 2, 6, 1, 5, 3, 7];
            case AdcChannelMode.Quad:
                return [0, 2, 4, 6, 1, 3, 5, 7];
            default:
                throw new NotImplementedException();
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
        var adcPP = GetThunderscopeAdcPeakPeakAtFrequencyLsq(channelIndex, frequency, sampleRateHz);

        var vPerBit = resolution switch
        {
            AdcResolution.EightBit => inputVpp / 256.0,
            AdcResolution.TwelveBit => inputVpp / 4096.0,
            _ => throw new NotImplementedException()
        };

        return adcPP * vPerBit;
    }

    public double GetThunderscopeAdcPeakPeakAtFrequencyLsq(int channelIndex, double frequency, double sampleRateHz)
    {
        var config = thunderScope!.GetConfiguration();
        if (!config.Acquisition.IsChannelIndexAnEnabledChannel(channelIndex))
            throw new TestbenchException("Requested channel index is not an enabled channel");

        var cycleCount = 5.0;
        var sampleCount = (int)Math.Round((sampleRateHz / frequency) * cycleCount);

        using SpanOwner<sbyte> i8Buffer = SpanOwner<sbyte>.Allocate(sampleCount);           // Returned to pool when it goes out of scope
        GetChannelDataI8(channelIndex, sampleCount, i8Buffer.Span);
        using SpanOwner<double> f64Buffer = SpanOwner<double>.Allocate(sampleCount);  // Returned to pool when it goes out of scope
        var i8Span = i8Buffer.Span;
        var f64Span = f64Buffer.Span;

        for (int i = 0; i < f64Span.Length; i++)
        {
            f64Span[i] = i8Span[i];
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

    private ReadOnlySpan<sbyte> GetDataI8Interleaved(int minTotalLength)
    {
        var config = thunderScope!.GetConfiguration();
        var channelCount = config.Acquisition.EnabledChannelsCount();
        if (channelCount == 0)
            throw new TestbenchException("No enabled channels");
        if (config.Acquisition.Resolution != AdcResolution.EightBit)
            throw new TestbenchException("Acquisition not set up up for 8 bit");
        if (minTotalLength % channelCount != 0)
            throw new TestbenchException("Total length must be a multiple of the number of enabled channels");

        var minimumAcquisitionLength = 1024 * 1024;
        var acquisitionLength = ((minTotalLength / minimumAcquisitionLength) + 1) * minimumAcquisitionLength;
        if (acquisitionLength < minimumAcquisitionLength)
            acquisitionLength = minimumAcquisitionLength;

        thunderScope!.Stop();
        thunderScope!.Start();
        var subsetDataMemory = dataMemory!.Subset(acquisitionLength);
        thunderScope!.Read(subsetDataMemory);
        return subsetDataMemory.DataSpanI8;
    }

    private ReadOnlySpan<sbyte> GetDataI8InterleavedExactLength(int totalLength)
    {
        var buffer = GetDataI8Interleaved(totalLength);
        // Buffer can be larger than requested length so use the end of the buffer
        return buffer.Slice(buffer.Length - totalLength, totalLength);
    }

    public void GetChannelDataI8(int channelIndex, int perChannelSampleCount, Span<sbyte> outputBuffer)
    {
        var config = thunderScope!.GetConfiguration();
        if (!config.Acquisition.IsChannelIndexAnEnabledChannel(channelIndex))
            throw new TestbenchException("Requested channel index is not an enabled channel");

        var channelCount = config.Acquisition.EnabledChannelsCount();
        var interleavedSampleCount = channelCount switch
        {
            1 => perChannelSampleCount,
            2 => perChannelSampleCount * 2,
            3 => perChannelSampleCount * 4,
            4 => perChannelSampleCount * 4,
            _ => throw new NotImplementedException()
        };

        var interleavedBuffer = GetDataI8Interleaved(interleavedSampleCount);
        var subsetShuffleMemory = shuffleMemory!.Subset(interleavedBuffer.Length);

        ReadOnlySpan<sbyte> channel;
        switch (config.Acquisition.EnabledChannelsCount())
        {
            case 1:
                {
                    channel = interleavedBuffer;
                    break;
                }
            case 2:
                {
                    ShuffleI8.TwoChannels(interleavedBuffer, subsetShuffleMemory.DataSpanI8);
                    var index = config.Acquisition.GetCaptureBufferIndexForTriggerChannel((TriggerChannel)(channelIndex + 1));
                    var length = interleavedBuffer.Length / 2;
                    channel = subsetShuffleMemory.DataSpanI8.Slice(index * length, length);
                    break;
                }
            case 3:
            case 4:
                {
                    ShuffleI8.FourChannels(interleavedBuffer, subsetShuffleMemory.DataSpanI8);
                    var index = config.Acquisition.GetCaptureBufferIndexForTriggerChannel((TriggerChannel)(channelIndex + 1));
                    var length = interleavedBuffer.Length / 4;
                    channel = subsetShuffleMemory.DataSpanI8.Slice(index * length, length);
                    break;
                }
            default:
                throw new NotImplementedException();
        }
        // Acquisition/shuffle buffer is likely larger than requested sampleCount so use the end of the acquisition buffer and copy to output buffer
        channel = channel.Slice(channel.Length - perChannelSampleCount, perChannelSampleCount);
        channel.CopyTo(outputBuffer);
    }

    public void EraseFactoryDataAndAppendHwid(ulong dna, Hwid hwid)
    {
        thunderScope?.FactoryDataErase(dna);
        uint tag = TagStr(Encoding.ASCII.GetBytes("HWID"));
        var json = hwid.ToDeviceJson();
        var jsonBytes = Encoding.ASCII.GetBytes(json);
        thunderScope?.FactoryDataAppend(tag, jsonBytes);
    }

    private static uint TagStr(byte[] x)
    {
        // libtslitex source:
        // #define TAGSTR(x)   (uint32_t)((x[0] << 24) + (x[1] << 16) + (x[2] << 8) + x[3])
        // Should be the same output as BinaryPrimitives.ReadUInt32BigEndian
        if (x is null) throw new ArgumentNullException(nameof(x));
        if (x.Length < 4) throw new ArgumentException("Tag must be at least 4 bytes.", nameof(x));
        return (uint)((x[0] << 24) + (x[1] << 16) + (x[2] << 8) + x[3]);
    }

    public void AppendFactoryCalibration(Calibration calibration)
    {
        uint tag = TagStr(Encoding.ASCII.GetBytes("FCAL"));
        var json = calibration.ToDeviceJson();
        var jsonBytes = Encoding.ASCII.GetBytes(json);
        thunderScope?.FactoryDataAppend(tag, jsonBytes);
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
