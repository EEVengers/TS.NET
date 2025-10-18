using TS.NET.Sequencer;

namespace TS.NET.Calibration;

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

    private ThunderscopeMemoryRegion? memoryRegion;

    public void Initialise(bool initSigGens)
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
        var thunderscopeCalibrationSettings = ThunderscopeCalibrationSettings.FromJsonFile(Variables.Instance.CalibrationFileName);


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
        hardwareConfig.Calibration[0] = thunderscopeCalibrationSettings.Channel1.ToDriver();
        hardwareConfig.Calibration[1] = thunderscopeCalibrationSettings.Channel2.ToDriver();
        hardwareConfig.Calibration[2] = thunderscopeCalibrationSettings.Channel3.ToDriver();
        hardwareConfig.Calibration[3] = thunderscopeCalibrationSettings.Channel4.ToDriver();
        hardwareConfig.AdcCalibration = thunderscopeCalibrationSettings.Adc.ToDriver();
        thunderScope = new Driver.Libtslitex.Thunderscope(loggerFactory, 1024 * 1024);
        thunderScope.Open(0);
        thunderScope.Configure(hardwareConfig, "");
        // Start to keep the device hot
        thunderScope.Start();
        memoryRegion = new ThunderscopeMemoryRegion(2);

        // Sig gen 1 (SDG2042X)
        if (Variables.Instance.SigGen1Ip != null && initSigGens)
        {
            sigGen1 = new TcpScpiConnection();
            sigGen1.Open(Variables.Instance.SigGen1Ip, 5025);
            Logger.Instance.Log(LogLevel.Debug, "SCPI connection to SDG2042X #1 opened.");
            sigGen1.WriteLine("*IDN?");
            var sigGen1Idn = sigGen1.ReadLine();
            Logger.Instance.Log(LogLevel.Debug, $"*IDN: {sigGen1Idn}");
            if (!sigGen1Idn.StartsWith("Siglent Technologies,SDG2042X", StringComparison.OrdinalIgnoreCase))
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
        }

        // Sig gen 2 (SDG2042X)
        if (Variables.Instance.SigGen2Ip != null && initSigGens)
        {
            sigGen2 = new TcpScpiConnection();
            sigGen2.Open(Variables.Instance.SigGen2Ip, 5025);
            Logger.Instance.Log(LogLevel.Debug, "SCPI connection to SDG2042X #2 opened.");
            sigGen2.WriteLine("*IDN?");
            var sigGen2Idn = sigGen2.ReadLine();
            Logger.Instance.Log(LogLevel.Debug, $"*IDN: {sigGen2Idn}");
            if (!sigGen2Idn.StartsWith("Siglent Technologies,SDG2042X", StringComparison.OrdinalIgnoreCase))
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
        if(thunderScope == null)
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
                    break;
                case 2:
                    thunderScope?.SetRate(500_000_000);
                    break;
                case 3:
                    thunderScope?.SetRate(250_000_000);
                    break;
                case 4:
                    thunderScope?.SetRate(250_000_000);
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

    public void SetThunderscopeRate(uint rateHz)
    {
        thunderScope?.SetRate(1_000_000_000);
        Thread.Sleep(Variables.Instance.FrontEndSettlingTimeMs);
    }

    //public void SetThunderscopeCalManual50R(int channelIndex, ushort dac, byte dpot, PgaPreampGain pgaPreampGain, byte pgaLadderAttenuation)
    //{
    //    thunderScope?.WriteLine($"CAL:FRONTEND CHAN{channelIndex + 1} DC 50 0 {dac} {dpot} {pgaLadderAttenuation} {(pgaPreampGain == PgaPreampGain.High ? "1" : "0")} 20M");
    //    Thread.Sleep(Variables.Instance.FrontEndSettlingTimeMs);
    //}

    public void SetThunderscopeCalManual50R(int channelIndex, ushort dac, byte dpot, PgaPreampGain pgaPreampGain, byte pgaLadderAttenuation)
    {
        var frontend = new ThunderscopeChannelFrontendManualControl
        {
            Coupling = ThunderscopeCoupling.DC,
            Termination = ThunderscopeTermination.FiftyOhm,
            Attenuator = 0,
            DAC = dac,
            DPOT = dpot,
            PgaLadderAttenuation = pgaLadderAttenuation,
            PgaFilter = ThunderscopeBandwidth.Bw20M,
            PgaHighGain = (pgaPreampGain == PgaPreampGain.High) ? (byte)1 : (byte)0
        };
        thunderScope?.SetChannelManualControl(channelIndex, frontend);
        Thread.Sleep(Variables.Instance.FrontEndSettlingTimeMs);
    }

    //public void SetThunderscopeCalManual1M(int channelIndex, ushort dac, byte dpot, PgaPreampGain pgaPreampGain, byte pgaLadderAttenuation)
    //{
    //    thunderScope?.WriteLine($"CAL:FRONTEND CHAN{channelIndex + 1} DC 1M 0 {dac} {dpot} {pgaLadderAttenuation} {(pgaPreampGain == PgaPreampGain.High ? "1" : "0")} 20M");
    //    Thread.Sleep(Variables.Instance.FrontEndSettlingTimeMs);
    //}

    public void SetThunderscopeCalManual1M(int channelIndex, ushort dac, byte dpot, PgaPreampGain pgaPreampGain, byte pgaLadderAttenuation)
    {
        var frontend = new ThunderscopeChannelFrontendManualControl
        {
            Coupling = ThunderscopeCoupling.DC,
            Termination = ThunderscopeTermination.OneMegaohm,
            Attenuator = 0,
            DAC = dac,
            DPOT = dpot,
            PgaLadderAttenuation = pgaLadderAttenuation,
            PgaFilter = ThunderscopeBandwidth.Bw20M,
            PgaHighGain = (pgaPreampGain == PgaPreampGain.High) ? (byte)1 : (byte)0
        };
        thunderScope?.SetChannelManualControl(channelIndex, frontend);
        Thread.Sleep(Variables.Instance.FrontEndSettlingTimeMs);
    }

    //public void SetThunderscopeCalManual1M(int channelIndex, bool attenuator, ushort dac, byte dpot, PgaPreampGain pgaPreampGain, byte pgaLadderAttenuation)
    //{
    //    thunderScope?.WriteLine($"CAL:FRONTEND CHAN{channelIndex + 1} DC 1M {(attenuator ? "1" : "0")} {dac} {dpot} {pgaLadderAttenuation} {(pgaPreampGain == PgaPreampGain.High ? "1" : "0")} 20M");
    //    Thread.Sleep(Variables.Instance.FrontEndSettlingTimeMs);
    //}

    public void SetThunderscopeCalManual1M(int channelIndex, bool attenuator, ushort dac, byte dpot, PgaPreampGain pgaPreampGain, byte pgaLadderAttenuation)
    {
        var frontend = new ThunderscopeChannelFrontendManualControl
        {
            Coupling = ThunderscopeCoupling.DC,
            Termination = ThunderscopeTermination.OneMegaohm,
            Attenuator = attenuator ? (byte)1 : (byte)0,
            DAC = dac,
            DPOT = dpot,
            PgaLadderAttenuation = pgaLadderAttenuation,
            PgaFilter = ThunderscopeBandwidth.Bw20M,
            PgaHighGain = (pgaPreampGain == PgaPreampGain.High) ? (byte)1 : (byte)0
        };
        thunderScope?.SetChannelManualControl(channelIndex, frontend);
        Thread.Sleep(Variables.Instance.FrontEndSettlingTimeMs);
    }

    public double GetThunderscopeAverage(int channelIndex)
    {
        GetThunderscopeStats(channelIndex, out var average, out _, out _);
        return average;
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

    public void GetThunderscopeStats(int channelIndex, out double average, out double min, out double max)
    {
        var memory = memoryRegion!.GetSegment(0);
        // To do: allow for multi-channel reading
        var config = thunderScope!.GetConfiguration();
        // Stop/start then flush out the buffers an arbitary amount. Exact amount of flushing required TBD.
        thunderScope!.Stop();
        thunderScope!.Start();
        for (int i = 0; i < 10; i++)
            thunderScope!.Read(memory, new CancellationToken());

        var samples = memory.DataSpanI8;

        Span<sbyte> channel;
        switch (config.EnabledChannelsCount())
        {
            case 1:
                channel = samples;
                break;
            case 2:
                {
                    Span<sbyte> twoChannels = memoryRegion!.GetSegment(1).DataSpanI8;
                    ShuffleI8.TwoChannels(samples, twoChannels);
                    var index = config.GetCaptureBufferIndexForTriggerChannel((TriggerChannel)(channelIndex + 1));
                    var length = samples.Length / 2;
                    channel = twoChannels.Slice(index * length, length);
                    break;
                }
            case 3:
            case 4:
                {
                    Span<sbyte> fourChannels = memoryRegion!.GetSegment(1).DataSpanI8;
                    ShuffleI8.FourChannels(samples, fourChannels);
                    var index = config.GetCaptureBufferIndexForTriggerChannel((TriggerChannel)(channelIndex + 1));
                    var length = samples.Length / 4;
                    channel = fourChannels.Slice(index * length, length);
                    break;
                }
            default:
                throw new NotImplementedException();
        }

        average = 0;
        min = int.MaxValue;
        max = int.MinValue;
        int sum = 0;
        foreach (var point in channel)
        {
            sum += point;
            if (point < min) min = point;
            if (point > max) max = point;
        }
        average = (double)sum / channel.Length;
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

    public void SetSdgDcOffset(int channelIndex, double voltage)
    {
        GetSdgReference(channelIndex, out var sigGen, out var sdgChannel);
        sigGen.WriteLine($"{sdgChannel}:BSWV OFST, {voltage:F4}"); Thread.Sleep(50);
    }

    public void SetSdgSine(int channelIndex, double vpp, uint freqHz)
    {
        GetSdgReference(channelIndex, out var sigGen, out var sdgChannel);
        sigGen.WriteLine($"{sdgChannel}:BSWV WVTP, SINE"); Thread.Sleep(50);
        sigGen.WriteLine($"{sdgChannel}:BSWV FRQ, {freqHz}"); Thread.Sleep(50);
        sigGen.WriteLine($"{sdgChannel}:BSWV AMP, {vpp}"); Thread.Sleep(50);
        sigGen.WriteLine($"{sdgChannel}:BSWV OFST, 0"); Thread.Sleep(50);
        SetSdgChannel(channelIndex);
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
