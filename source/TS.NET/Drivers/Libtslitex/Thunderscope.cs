using Microsoft.Extensions.Logging;

namespace TS.NET.Driver.Libtslitex;

public record ThunderscopeLiteXDevice(uint DeviceID, uint HardwareRev, uint GatewareRev, uint LitexRev, string DevicePath, string Identity, string Serial, string BuildConfiguration, string BuildDate, string ManufacturingSignature);

public class Thunderscope : IThunderscope
{
    private readonly ILogger logger;
    private bool open = false;
    private bool running = false;
    private nint tsHandle;
    private uint dmaBufferSize;

    private int[] channelsEnabled;
    private bool[] channelManualOverride;
    private Calibration calibration;
    private ThunderscopeChannelFrontend[] channelFrontend;

    uint cachedSampleRateHz = 1_000_000_000;
    AdcResolution cachedSampleResolution = AdcResolution.EightBit;
    AdcChannelMode cachedAdcChannelMode = AdcChannelMode.Single;
    ThunderscopeRefClockMode cachedRefClockMode = ThunderscopeRefClockMode.Disabled;
    uint cachedRefClockFrequencyHz = 10_000_000;

    private bool beta = false;

    private CancellationTokenSource? cancelTokenSource = null;
    private Task? taskMonitoring = null;

    double lastFrontendUpdateTemp = 25.0;

    public static IReadOnlyList<ThunderscopeLiteXDevice> ListDevices()
    {
        var devices = new List<ThunderscopeLiteXDevice>();
        uint i = 0;
        while (Interop.ListDevices(i, out var devInfo) == 0)
        {
            i++;
            devices.Add(new ThunderscopeLiteXDevice(devInfo.deviceID, devInfo.hw_id, devInfo.gw_id, devInfo.litex, devInfo.devicePath, devInfo.identity, devInfo.serialNumber, devInfo.buildConfig, devInfo.buildDate, devInfo.mfgSignature));
        }
        return devices;
    }

    public Thunderscope(ILoggerFactory loggerFactory, int dmaBufferSize)
    {
        this.dmaBufferSize = (uint)dmaBufferSize;
        logger = loggerFactory.CreateLogger("Driver.LiteX");
        channelsEnabled = new int[4];
        channelManualOverride = new bool[4];
        calibration = new Calibration();
        channelFrontend = new ThunderscopeChannelFrontend[4];
    }

    public void Open(uint devIndex)
    {
        if (open)
            Close();

        tsHandle = Interop.Open(devIndex, false);

        if (tsHandle == 0)
            throw new ThunderscopeException($"Failed to open device {devIndex} ({tsHandle})");
        open = true;
    }

    public void Configure(ThunderscopeHardwareConfig initialHardwareConfiguration, Calibration calibration, string hardwareRevision)
    {
        CheckOpen();

        if (hardwareRevision.Equals("Rev4.1", StringComparison.InvariantCultureIgnoreCase))
            beta = true;

        //SetAdcCalibration(initialHardwareConfiguration.AdcCalibration);
        //for (int chan = 0; chan < 4; chan++)
        //{
        //    SetChannelCalibration(chan, initialHardwareConfiguration.Calibration[chan]);
        //}
        this.calibration = calibration;
        GetStatus();

        for (int chan = 0; chan < 4; chan++)
        {
            channelFrontend[chan] = initialHardwareConfiguration.Frontend[chan];
            var chanEnabled = ((initialHardwareConfiguration.Acquisition.EnabledChannels >> chan) & 0x01) > 0;
            SetChannelEnable(chan, chanEnabled, updateFrontends: false);
        }

        SetSampleMode(initialHardwareConfiguration.Acquisition.SampleRateHz, initialHardwareConfiguration.Acquisition.Resolution, updateFrontends: false);
        UpdateFrontends();
        SetExtSyncMode(initialHardwareConfiguration.ExtSyncMode);
        SetRefClockMode(initialHardwareConfiguration.RefClockMode);
        SetRefClockFrequency(initialHardwareConfiguration.RefClockFrequencyHz);

        var temperature = GetStatus().FpgaTemp;
        var frontend = calibration.Frontend[0];
        foreach (var path in frontend.Path)
        {
            Frontend.CalculateAllowableOffsetRangeV(logger, frontend, path, attenuator: false, temperature, out var minOffsetV, out var maxOffsetV);
            logger.LogDebug($"Input range: {path.BufferInputVpp:F3}Vpp, input offset: {minOffsetV:F3}V to {maxOffsetV:F3}V [{path.PgaPreampGain}, {path.PgaLadder}, attenuator off]");
        }

        foreach (var path in frontend.Path)
        {
            Frontend.CalculateAllowableOffsetRangeV(logger, frontend, path, attenuator: true, temperature, out var minOffsetV, out var maxOffsetV);
            logger.LogDebug($"Input range: {path.BufferInputVpp / frontend.AttenuatorScale:F3}Vpp, input offset: {minOffsetV:F3}V to {maxOffsetV:F3}V [{path.PgaPreampGain}, {path.PgaLadder}, attenuator on]");
        }
    }

    public void Close()
    {
        CheckOpen();
        Stop();

        if (taskMonitoring != null)
        {
            cancelTokenSource?.Cancel();
            taskMonitoring.Wait();
            taskMonitoring = null;
        }

        var retVal = Interop.Close(tsHandle);
        if (retVal < 0)
            throw new ThunderscopeException($"Failed closing device ({GetLibraryReturnString(retVal)})");

        open = false;
    }

    public void Start()
    {
        Start(timeoutSec: 1);
    }

    public void Start(int timeoutSec)
    {
        CheckOpen();

        if (!running)
        {
            DateTimeOffset start = DateTimeOffset.UtcNow;
            while (true)
            {
                if (GetStatus().AdcFrameSync)
                    break;
                if (DateTimeOffset.UtcNow.Subtract(start).TotalSeconds >= timeoutSec)
                    throw new ThunderscopeException("Timeout when starting, ADC frame sync failed");
                else
                    Thread.Sleep(10);
            }

            var retVal = Interop.DataEnable(tsHandle, 1);
            if (retVal < 0)
                throw new ThunderscopeException($"Could not start ({GetLibraryReturnString(retVal)})");
        }

        running = true;
    }

    public void StartMonitoring()
    {
        CheckOpen();

        if (taskMonitoring == null)
        {
            cancelTokenSource = new CancellationTokenSource();
            taskMonitoring = Task.Factory.StartNew(() => MonitoringLoop(logger: logger, cancelToken: cancelTokenSource.Token), TaskCreationOptions.LongRunning);
        }
    }

    public void Stop()
    {
        CheckOpen();

        if (running)
        {
            var retVal = Interop.DataEnable(tsHandle, 0);
            if (retVal < 0)
                throw new ThunderscopeException($"Could not stop ({GetLibraryReturnString(retVal)})");
        }

        running = false;
    }

    public bool Running()
    {
        return running;
    }

    public void Read(ThunderscopeMemory data)
    {
        CheckOpen();
        if (data.LengthBytes % dmaBufferSize != 0)
            throw new ThunderscopeException("Read length not supported by driver, must be multiple of DMA_BUFFER_SIZE");

        unsafe
        {
            int readLen = Interop.Read(tsHandle, data.DataLoadPointer, (uint)data.LengthBytes);
            if (readLen < 0)
                throw new ThunderscopeException($"Failed to read samples ({readLen})");
            else if (readLen != data.LengthBytes)
                throw new ThunderscopeException($"Read incorrect sample length ({readLen})");
        }
    }

    public bool TryRead(Span<byte> data, out ulong sampleStartIndex, out int sampleLengthPerChannel)
    {
        if (!open)
        {
            sampleStartIndex = 0;
            sampleLengthPerChannel = 0;
            return false;
        }
        if (data.Length % dmaBufferSize != 0)
            throw new ThunderscopeException("Read length not supported by driver, must be multiple of DMA_BUFFER_SIZE");
        unsafe
        {
            fixed (byte* dataP = data)
            {
                int readLen = Interop.Read(tsHandle, dataP, (uint)data.Length, out sampleStartIndex);
                if (readLen < 0)
                {
                    sampleStartIndex = 0;
                    sampleLengthPerChannel = 0;
                    return false;
                }
                else if (readLen != data.Length)
                    throw new ThunderscopeException($"Read incorrect sample length ({readLen})");
            }
            sampleLengthPerChannel = cachedAdcChannelMode switch
            {
                AdcChannelMode.Single => data.Length,
                AdcChannelMode.Dual => data.Length / 2,
                AdcChannelMode.Quad => data.Length / 4,
                _ => throw new NotImplementedException()
            };
            if (cachedSampleResolution == AdcResolution.TwelveBit)
                sampleLengthPerChannel /= 2;
            return true;
        }
    }

    public ThunderscopeChannelFrontend GetChannelFrontend(int channelIndex)
    {
        CheckOpen();

        //var channel = new ThunderscopeChannelFrontend();
        //var tsChannel = new Interop.tsChannelParam_t();

        //var retVal = Interop.GetChannelConfig(tsHandle, (uint)channelIndex, out tsChannel);
        //if (retVal < 0)
        //    throw new ThunderscopeException($"Failed to get channel {channelIndex} config ({GetLibraryReturnString(retVal)})");

        //channel.RequestedVoltFullScale = requestedChannelVoltScale[channelIndex];
        //channel.ActualVoltFullScale = (double)tsChannel.volt_scale_uV / 1000000.0;
        //channel.RequestedVoltOffset = requestedChannelVoltOffset[channelIndex];
        //channel.ActualVoltOffset = (double)tsChannel.volt_offset_uV / 1000000.0;
        //channel.Coupling = (tsChannel.coupling == 1) ? ThunderscopeCoupling.AC : ThunderscopeCoupling.DC;
        //channel.Termination = (tsChannel.term == 1) ? ThunderscopeTermination.FiftyOhm : ThunderscopeTermination.OneMegaohm;
        //channel.Bandwidth = (tsChannel.bandwidth == 750) ? ThunderscopeBandwidth.Bw750M :
        //                        (tsChannel.bandwidth == 650) ? ThunderscopeBandwidth.Bw650M :
        //                        (tsChannel.bandwidth == 350) ? ThunderscopeBandwidth.Bw350M :
        //                        (tsChannel.bandwidth == 200) ? ThunderscopeBandwidth.Bw200M :
        //                        (tsChannel.bandwidth == 100) ? ThunderscopeBandwidth.Bw100M :
        //                        (tsChannel.bandwidth == 20) ? ThunderscopeBandwidth.Bw20M :
        //                        ThunderscopeBandwidth.BwFull;

        //return channel;


        var channel = channelFrontend[channelIndex];
        //var tsChannel = new Interop.tsChannelParam_t();

        //var retVal = Interop.GetChannelConfig(tsHandle, (uint)channelIndex, out tsChannel);
        //if (retVal < 0)
        //    throw new ThunderscopeException($"Failed to get channel {channelIndex} config ({GetLibraryReturnString(retVal)})");
        //channel.Termination = tsChannel.term switch
        //{
        //    0 => ThunderscopeTermination.OneMegaohm,
        //    1 => ThunderscopeTermination.FiftyOhm,
        //    _ => throw new NotImplementedException()
        //};
        return channel;
    }

    public ThunderscopeHardwareConfig GetConfiguration()
    {
        CheckOpen();

        var config = new ThunderscopeHardwareConfig();

        channelsEnabled = [];
        for (int channelIndex = 0; channelIndex < 4; channelIndex++)
        {
            config.Frontend[channelIndex] = GetChannelFrontend(channelIndex);

            var tsChannel = new Interop.tsChannelParam_t();
            var retVal = Interop.GetChannelConfig(tsHandle, (uint)channelIndex, out tsChannel);
            if (retVal < 0)
                throw new ThunderscopeException($"Failed to get channel {channelIndex} config ({GetLibraryReturnString(retVal)})");

            // This class should be tracking enabled channels, override here anyway
            if (tsChannel.active == 1)
            {
                channelsEnabled = [.. channelsEnabled, (byte)channelIndex];
            }
        }
        config.Acquisition = GetAcquisitionConfig();
        return config;
    }

    private ThunderscopeAcquisitionConfig GetAcquisitionConfig()
    {
        var acquisitionConfig = new ThunderscopeAcquisitionConfig();
        var channelCount = channelsEnabled.Length;
        acquisitionConfig.AdcChannelMode = (channelCount == 1) ? AdcChannelMode.Single :
                    (channelCount == 2) ? AdcChannelMode.Dual :
                    AdcChannelMode.Quad;
        for (int i = 0; i < 4; i++)
        {
            if (channelsEnabled.Contains((byte)i))
                acquisitionConfig.EnabledChannels |= (byte)(1 << i);
        }
        GetStatus();
        acquisitionConfig.SampleRateHz = cachedSampleRateHz;
        acquisitionConfig.Resolution = cachedSampleResolution;
        cachedAdcChannelMode = acquisitionConfig.AdcChannelMode;
        return acquisitionConfig;
    }

    //public ThunderscopeChannelCalibration GetChannelCalibration(int channelIndex)
    //{
    //    CheckOpen();

    //    if (channelIndex >= 4 || channelIndex < 0)
    //        throw new ThunderscopeException($"Invalid Channel Index {channelIndex}");

    //    return channelCalibration[channelIndex];
    //}

    public ThunderscopeLiteXStatus GetStatus()
    {
        CheckOpen();

        var litexState = new Interop.tsScopeState_t();
        var retVal = Interop.GetStatus(tsHandle, out litexState);
        if (retVal < 0)
            throw new ThunderscopeException($"Failed to get libtslitex status ({GetLibraryReturnString(retVal)})");

        var health = new ThunderscopeLiteXStatus();
        health.AdcSampleRate = litexState.adc_sample_rate;
        health.AdcSampleSize = litexState.adc_sample_bits;
        health.AdcSampleResolution = litexState.adc_sample_resolution;
        health.AdcSamplesLost = litexState.adc_lost_buffer_count;
        health.AdcFrameSync = (litexState.flags & 0x2) > 0;
        health.RefClockInValid = (litexState.flags & 0x40) > 0;
        health.FpgaTemp = litexState.temp_c / 1000.0;
        health.VccInt = litexState.vcc_int / 1000.0;
        health.VccAux = litexState.vcc_aux / 1000.0;
        health.VccBram = litexState.vcc_bram / 1000.0;
        cachedSampleRateHz = health.AdcSampleRate;
        cachedSampleResolution = health.AdcSampleResolution == 256 ? AdcResolution.EightBit : AdcResolution.TwelveBit;

        return health;
    }

    public void SetRate(ulong sampleRateHz)
    {
        SetSampleMode(sampleRateHz, cachedSampleResolution, updateFrontends: true);
    }

    public void SetResolution(AdcResolution resolution)
    {
        uint sampleRateHz = cachedSampleRateHz;
        if (resolution == AdcResolution.TwelveBit && sampleRateHz > 660_000_000)
            sampleRateHz = 660_000_000;

        SetSampleMode(sampleRateHz, resolution, updateFrontends: true);
    }

    public void SetChannelFrontend(int channelIndex, ThunderscopeChannelFrontend channel)
    {
        CheckOpen();

        //var tsChannel = new Interop.tsChannelParam_t();
        //var retVal = Interop.GetChannelConfig(tsHandle, (uint)channelIndex, out tsChannel);

        //if (retVal < 0)
        //    throw new ThunderscopeException($"Failed to get channel {channelIndex} config ({GetLibraryReturnString(retVal)})");

        //tsChannel.volt_scale_uV = (uint)(channel.RequestedVoltFullScale * 1000000);
        //tsChannel.volt_offset_uV = (int)(channel.RequestedVoltOffset * 1000000);
        //tsChannel.coupling = (channel.Coupling == ThunderscopeCoupling.DC) ? (byte)0 : (byte)1;
        //tsChannel.term = (channel.Termination == ThunderscopeTermination.OneMegaohm) ? (byte)0 : (byte)1;
        //tsChannel.bandwidth = channel.Bandwidth switch
        //{
        //    ThunderscopeBandwidth.BwFull => 900,
        //    ThunderscopeBandwidth.Bw750M => 750,
        //    ThunderscopeBandwidth.Bw650M => 650,
        //    ThunderscopeBandwidth.Bw350M => 350,
        //    ThunderscopeBandwidth.Bw200M => 200,
        //    ThunderscopeBandwidth.Bw100M => 100,
        //    ThunderscopeBandwidth.Bw20M => 20,
        //    _ => throw new NotImplementedException()
        //};

        //retVal = Interop.SetChannelConfig(tsHandle, (uint)channelIndex, in tsChannel);

        //if (retVal < 0)
        //    throw new ThunderscopeException($"Failed to set channel {channelIndex} config ({GetLibraryReturnString(retVal)})");

        //requestedChannelVoltScale[channelIndex] = channel.RequestedVoltFullScale;
        //requestedChannelVoltOffset[channelIndex] = channel.RequestedVoltOffset;

        var loadScales = Frontend.GetAllLoadScales(calibration, channelsEnabled, cachedSampleRateHz);
        SetChannelFrontend(channelIndex, channel, loadScales[channelIndex]);
    }

    public void SetAdcBranchGainManualControl(byte[] branchGain)
    {
        CheckOpen();

        var tsCal = new Interop.tsAdcCalibration_t();
        tsCal.branchFineGain[0] = branchGain[0];
        tsCal.branchFineGain[1] = branchGain[1];
        tsCal.branchFineGain[2] = branchGain[2];
        tsCal.branchFineGain[3] = branchGain[3];
        tsCal.branchFineGain[4] = branchGain[4];
        tsCal.branchFineGain[5] = branchGain[5];
        tsCal.branchFineGain[6] = branchGain[6];
        tsCal.branchFineGain[7] = branchGain[7];

        Interop.SetAdcCalibration(tsHandle, in tsCal);
    }

    //public void SetChannelCalibration(int channelIndex, ThunderscopeChannelCalibration channelCalibration)
    //{
    //    CheckOpen();

    //    if (channelIndex >= 4 || channelIndex < 0)
    //        throw new ThunderscopeException($"Invalid Channel Index {channelIndex}");

    //    var tsCal = new Interop.tsChannelCalibration_t
    //    {
    //        //buffer_uV = (int)(channelCalibration.BufferOffset * 1000000),
    //        //bias_uV = (int)(channelCalibration.BiasVoltage * 1000000),
    //        //attenuatorGain1M_mdB = (int)(channelCalibration.AttenuatorGain1MOhm * 1000),
    //        //attenuatorGain50_mdB = (int)(channelCalibration.AttenuatorGain50Ohm * 1000),
    //        //bufferGain_mdB = (int)(channelCalibration.BufferGain * 1000),
    //        //trimRheostat_range = (int)channelCalibration.TrimResistorOhms,
    //        //preampLowGainError_mdB = (int)(channelCalibration.PgaLowGainError * 1000),
    //        //preampHighGainError_mdB = (int)(channelCalibration.PgaHighGainError * 1000),
    //        //preampLowOffset_uV = (int)(channelCalibration.PgaLowOffsetVoltage * 1000000),
    //        //preampHighOffset_uV = (int)(channelCalibration.PgaHighOffsetVoltage * 1000000),
    //        //preampOutputGainError_mdB = (int)(channelCalibration.PgaOutputGainError * 1000),
    //        //preampInputBias_uA = (int)channelCalibration.PgaInputBiasCurrent,
    //    };
    //    //tsCal.preampAttenuatorGain_mdB[0] = (int)channelCalibration.PgaAttenuatorGain0;
    //    //tsCal.preampAttenuatorGain_mdB[1] = (int)channelCalibration.PgaAttenuatorGain1;
    //    //tsCal.preampAttenuatorGain_mdB[2] = (int)channelCalibration.PgaAttenuatorGain2;
    //    //tsCal.preampAttenuatorGain_mdB[3] = (int)channelCalibration.PgaAttenuatorGain3;
    //    //tsCal.preampAttenuatorGain_mdB[4] = (int)channelCalibration.PgaAttenuatorGain4;
    //    //tsCal.preampAttenuatorGain_mdB[5] = (int)channelCalibration.PgaAttenuatorGain5;
    //    //tsCal.preampAttenuatorGain_mdB[6] = (int)channelCalibration.PgaAttenuatorGain6;
    //    //tsCal.preampAttenuatorGain_mdB[7] = (int)channelCalibration.PgaAttenuatorGain7;
    //    //tsCal.preampAttenuatorGain_mdB[8] = (int)channelCalibration.PgaAttenuatorGain8;
    //    //tsCal.preampAttenuatorGain_mdB[9] = (int)channelCalibration.PgaAttenuatorGain9;
    //    //tsCal.preampAttenuatorGain_mdB[10] = (int)channelCalibration.PgaAttenuatorGain10;

    //    this.channelCalibration[channelIndex] = channelCalibration;
    //    Interop.SetCalibration(tsHandle, (uint)channelIndex, in tsCal);
    //}

    public void SetChannelEnable(int channelIndex, bool enabled) => SetChannelEnable(channelIndex, enabled, updateFrontends: true);

    /// <summary>
    /// Intended for use by testbench/calibration routines that use SetChannelManualControl
    /// </summary>
    public void SetChannelEnable(int channelIndex, bool enabled, bool updateFrontends)
    {
        CheckOpen();

        var restart = running;
        if (restart)
            Stop();

        var tsChannel = new Interop.tsChannelParam_t();
        var retVal = Interop.GetChannelConfig(tsHandle, (uint)channelIndex, out tsChannel);

        if (retVal < 0)
            throw new ThunderscopeException($"Failed to get channel {channelIndex} config ({GetLibraryReturnString(retVal)})");

        tsChannel.active = enabled ? (byte)1 : (byte)0;

        retVal = Interop.SetChannelConfig(tsHandle, (uint)channelIndex, in tsChannel);

        if (retVal < 0)
            throw new ThunderscopeException($"Failed to set channel {channelIndex} config ({GetLibraryReturnString(retVal)})");

        if (enabled)
        {
            channelsEnabled = channelsEnabled.Where(c => c != channelIndex).Append((byte)channelIndex).Order().ToArray();
        }
        else
        {
            channelsEnabled = channelsEnabled.Where(c => c != channelIndex).ToArray();
        }

        // There is a channel-count vs. scale relationship so update frontends
        if (updateFrontends)
            UpdateFrontends();

        if (restart)
            Start();

        GetAcquisitionConfig();     // Update cachedAdcChannelMode
    }

    public void SetChannelManualControl(int channelIndex, ThunderscopeChannelFrontendManualControl channel)
    {
        CheckOpen();

        var tsChannel = new Interop.tsChannelCtrl_t();
        tsChannel.atten = channel.Attenuator;
        tsChannel.term = (channel.Termination == ThunderscopeTermination.OneMegaohm) ? (byte)0 : (byte)1;
        tsChannel.dc_couple = (channel.Coupling == ThunderscopeCoupling.DC) ? (byte)1 : (byte)0;
        tsChannel.dac = channel.DAC;
        tsChannel.dpot = channel.DPOT;

        tsChannel.pga_atten = channel.PgaLadderAttenuation;
        tsChannel.pga_high_gain = channel.PgaHighGain;
        tsChannel.pga_bw = (byte)channel.PgaFilter;

        var retVal = Interop.SetChannelManualControl(tsHandle, (uint)channelIndex, tsChannel);

        if (retVal < 0)
            throw new ThunderscopeException($"Failed to set channel {channelIndex} config ({GetLibraryReturnString(retVal)})");

        channelManualOverride[channelIndex] = true;
    }

    public bool TryGetEvent(out ThunderscopeEvent thunderscopeEvent, out ulong eventSampleIndex)
    {
        CheckOpen();
        var retVal = Interop.GetEvent(tsHandle, out var tsEvent);
        if (retVal < 0)
            throw new ThunderscopeException($"Failed to get event ({GetLibraryReturnString(retVal)})");
        switch (tsEvent.type)
        {
            case Interop.tsEventType_t.TS_EVT_HOST_SW:
                thunderscopeEvent = ThunderscopeEvent.SyncOutputRisingEdge;
                eventSampleIndex = tsEvent.index;
                return true;
            case Interop.tsEventType_t.TS_EVT_EXT_SYNC:
                thunderscopeEvent = ThunderscopeEvent.SyncInputRisingEdge;
                eventSampleIndex = tsEvent.index;
                return true;
            default:
                thunderscopeEvent = 0;
                eventSampleIndex = 0;
                return false;
        }
    }

    public void AssertEvent()
    {
        CheckOpen();
        var retVal = Interop.AssertEventSync(tsHandle);
        if (retVal < 0)
            throw new ThunderscopeException($"Failed to assert event ({GetLibraryReturnString(retVal)})");
    }

    public void SetPeriodicEventSync(uint periodMicrosec)
    {
        CheckOpen();
        var retVal = Interop.ConfigurePeriodicEventSync(tsHandle, periodMicrosec);
        if (retVal < 0)
            throw new ThunderscopeException($"Failed to set periodic event sync ({GetLibraryReturnString(retVal)})");
    }

    public void SetExtSyncMode(ThunderscopeExtSyncMode extSyncMode)
    {
        CheckOpen();
        var retVal = Interop.ConfigureExtSync(tsHandle, (Interop.tsSyncMode_t)extSyncMode);
        if (retVal < 0)
            throw new ThunderscopeException($"Failed to set external sync mode ({GetLibraryReturnString(retVal)})");
    }

    public void SetRefClockMode(ThunderscopeRefClockMode refClockMode)
    {
        CheckOpen();
        var retVal = Interop.ConfigureRefClock(tsHandle, (Interop.tsRefClockMode_t)refClockMode, cachedRefClockFrequencyHz);
        if (retVal < 0)
            throw new ThunderscopeException($"Failed to set reference clock mode ({GetLibraryReturnString(retVal)})");
        cachedRefClockMode = refClockMode;
    }

    public void SetRefClockFrequency(uint refClockFrequencyHz)
    {
        CheckOpen();
        var retVal = Interop.ConfigureRefClock(tsHandle, (Interop.tsRefClockMode_t)cachedRefClockMode, refClockFrequencyHz);
        if (retVal < 0)
            throw new ThunderscopeException($"Failed to set reference clock frequency ({GetLibraryReturnString(retVal)})");
        cachedRefClockFrequencyHz = refClockFrequencyHz;
    }

    public int UserDataRead(Span<byte> buffer, uint offset)
    {
        unsafe
        {
            fixed (byte* bufferP = buffer)
            {
                var retVal = Interop.UserDataRead(tsHandle, bufferP, offset, (uint)buffer.Length);
                if (retVal < 0)
                    throw new ThunderscopeException($"Failed to read user data ({GetLibraryReturnString(retVal)})");
                return retVal;
            }
        }
    }

    public int UserDataWrite(Span<byte> buffer, uint offset)
    {
        unsafe
        {
            fixed (byte* bufferP = buffer)
            {
                var retVal = Interop.UserDataWrite(tsHandle, bufferP, offset, (uint)buffer.Length);
                if (retVal < 0)
                    throw new ThunderscopeException($"Failed to write user data ({GetLibraryReturnString(retVal)})");
                return retVal;
            }
        }
    }

    public int FactoryDataErase(ulong dna)
    {
        var retVal = Interop.FactoryDataErase(tsHandle, dna);
        if (retVal < 0)
            throw new ThunderscopeException($"Failed to erase factory data ({GetLibraryReturnString(retVal)})");
        return retVal;
    }

    public int FactoryDataAppend(uint tag, Span<byte> buffer)
    {
        unsafe
        {
            fixed (byte* bufferP = buffer)
            {
                var retVal = Interop.FactoryDataAppend(tsHandle, tag, (uint)buffer.Length, bufferP);
                if (retVal < 0)
                    throw new ThunderscopeException($"Failed to append factory data ({GetLibraryReturnString(retVal)})");
                return retVal;
            }
        }
    }

    private void SetChannelFrontend(int channelIndex, ThunderscopeChannelFrontend channel, double channelLoadScale)
    {
        CheckOpen();

        lastFrontendUpdateTemp = GetStatus().FpgaTemp;
        var frontend = calibration.Frontend[channelIndex];

        var calculatedChannel = Frontend.CalculateFrontend(logger, channelIndex, channel, frontend, channelLoadScale, cachedSampleRateHz, lastFrontendUpdateTemp, beta, out var manualControl);
        SetChannelManualControl(channelIndex, manualControl);
        channelManualOverride[channelIndex] = false;            // SetChannelManualControl sets to true, so immediately set to false
        channelFrontend[channelIndex] = calculatedChannel;
    }

    private void UpdateFrontends()
    {
        GetStatus();    // Update cachedSampleRateHz & cachedSampleResolution

        var loadScales = Frontend.GetAllLoadScales(calibration, channelsEnabled, cachedSampleRateHz);
        for (int i = 0; i < channelFrontend.Length; i++)
        {
            // Update all frontends that aren't under manual override, even for disabled channels (so relays actuate when expected)
            if (!channelManualOverride[i])
            {
                Console.WriteLine($"Updating channel {i} frontend with load scale {loadScales[i]:F3}");
                SetChannelFrontend(i, channelFrontend[i], loadScales[i]);
            }
        }
        var branchGains = Frontend.GetAdcBranchGain(calibration, channelsEnabled, cachedSampleRateHz);
        SetAdcBranchGainManualControl(branchGains);
    }

    private void CheckOpen()
    {
        if (!open)
            throw new ThunderscopeException("Thunderscope not open");
    }

    private static string GetLibraryReturnString(int retValue)
    {
        //#define TS_STATUS_OK                (0)
        //#define TS_STATUS_ERROR             (-1)
        //#define TS_INVALID_PARAM            (-2)
        return retValue switch
        {
            0 => "TS_STATUS_OK",
            -1 => "TS_STATUS_ERROR",
            -2 => "TS_INVALID_PARAM",
            _ => "Unknown"
        };
    }

    private void SetSampleMode(ulong sampleRateHz, AdcResolution resolution, bool updateFrontends)
    {
        CheckOpen();

        var restart = running;
        if (restart)
            Stop();

        var format = resolution switch
        {
            AdcResolution.EightBit => Interop.tsSampleFormat_t.Format8Bit,
            AdcResolution.TwelveBit => Interop.tsSampleFormat_t.Format12BitLSB,
            //AdcResolution.TwelveBit => Interop.tsSampleFormat_t.Format12BitMSB,
            _ => throw new NotImplementedException()
        };
        var retVal = Interop.SetSampleMode(tsHandle, (uint)sampleRateHz, format);

        // There is a rate vs. scale relationship so update frontends
        if (updateFrontends)
            UpdateFrontends();

        if (retVal == -2)
            logger.LogTrace($"Failed to set sample rate ({sampleRateHz}): {GetLibraryReturnString(retVal)}");
        else if (retVal < 0)
            throw new ThunderscopeException($"Error trying to set sample rate {sampleRateHz} ({GetLibraryReturnString(retVal)})");

        if (restart)
            Start();
    }

    private void MonitoringLoop(ILogger logger, CancellationToken cancelToken)
    {
        try
        {
            Thread.CurrentThread.Name = "Monitoring";

            const double tempDelta = 0.4;               // FPGA temp has resolution of 0.123C so should't go below 0.369C (noise will trigger constant updating)
            lastFrontendUpdateTemp = GetStatus().FpgaTemp;        // Loop is initially started in Configure so device should be open at this point
            logger.LogDebug("{MonitoringLoop} started", nameof(MonitoringLoop));

            while (!cancelToken.IsCancellationRequested)
            {
                if (open && running)
                {
                    var currentTemp = GetStatus().FpgaTemp;
                    var diff = Math.Abs(currentTemp - lastFrontendUpdateTemp);
                    logger.LogDebug($"FPGA temp change since last frontend update {diff:F3}C (current: {currentTemp:F3}C)");

                    if (diff > tempDelta)
                    {
                        for (int channelIndex = 0; channelIndex < 4; channelIndex++)
                        {
                            var frontend = GetChannelFrontend(channelIndex);
                            SetChannelFrontend(channelIndex, frontend);         // Updates lastFrontendUpdateTemp
                        }
                    }
                }

                if (cancelToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(3)))
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogDebug("{MonitoringLoop} stopping...", nameof(MonitoringLoop));
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Error");
        }
        finally
        {
            logger.LogDebug("{MonitoringLoop} stopped...", nameof(MonitoringLoop));
        }
    }
}
