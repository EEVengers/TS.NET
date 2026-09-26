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
    private ThunderscopeLiteXDevice tsInfo;

    uint cachedSampleRateHz = 1_000_000_000;
    AdcResolution cachedSampleResolution = AdcResolution.EightBit;
    AdcChannelMode cachedAdcChannelMode = AdcChannelMode.Single;
    ThunderscopeRefClockMode cachedRefClockMode = ThunderscopeRefClockMode.Disabled;
    uint cachedRefClockFrequencyHz = 10_000_000;

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
        tsInfo = new ThunderscopeLiteXDevice(0, 0, 0, 0, "", "", "", "", "", "");
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

    public void SetCalibration(Calibration calibration)
    {
        CheckOpen();

        SetAdcCalibration(calibration.Adc);
        for (int chan = 0; chan < 4; chan++)
        {
            SetFrontendCalibration(chan, calibration.Frontend[chan]);
        }
        this.calibration = calibration;
    }
    public void Configure(ThunderscopeHardwareConfig initialHardwareConfiguration)
    {
        CheckOpen();

        GetStatus();

        for (int chan = 0; chan < 4; chan++)
        {
            channelFrontend[chan] = initialHardwareConfiguration.Frontend[chan];
            var chanEnabled = ((initialHardwareConfiguration.Acquisition.EnabledChannels >> chan) & 0x01) > 0;
            SetChannelEnable(chan, chanEnabled);
        }

        SetSampleMode(initialHardwareConfiguration.Acquisition.SampleRateHz, initialHardwareConfiguration.Acquisition.Resolution);
        UpdateFrontends();
        SetExtSyncMode(initialHardwareConfiguration.ExtSyncMode);
        SetRefClockMode(initialHardwareConfiguration.RefClockMode);
        SetRefClockFrequency(initialHardwareConfiguration.RefClockFrequencyHz);

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
            
            RefreshFrontendValues();
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

        var channel = new ThunderscopeChannelFrontend();
        var tsChannel = new Interop.tsChannelParam_t();

        var retVal = Interop.GetChannelConfig(tsHandle, (uint)channelIndex, out tsChannel);
        if (retVal < 0)
            throw new ThunderscopeException($"Failed to get channel {channelIndex} config ({GetLibraryReturnString(retVal)})");

        channel.RequestedVoltFullScale = channelFrontend[channelIndex].RequestedVoltFullScale;
        channel.ActualVoltFullScale = tsChannel.volt_scale_uV / 1000000.0;
        channel.RequestedVoltOffset = channelFrontend[channelIndex].RequestedVoltOffset;
        channel.ActualVoltOffset = tsChannel.volt_offset_uV / 1000000.0;
        channel.Coupling = (tsChannel.coupling == 1) ? ThunderscopeCoupling.AC : ThunderscopeCoupling.DC;
        channel.RequestedTermination = channelFrontend[channelIndex].RequestedTermination;
        channel.ActualTermination = (tsChannel.term == 1) ? ThunderscopeTermination.FiftyOhm : ThunderscopeTermination.OneMegaohm;
        channel.Bandwidth = tsChannel.bandwidth switch
        {
            750 => ThunderscopeBandwidth.Bw750M,
            650 => ThunderscopeBandwidth.Bw650M,
            350 => ThunderscopeBandwidth.Bw350M,
            200 => ThunderscopeBandwidth.Bw200M,
            100 => ThunderscopeBandwidth.Bw100M,
            20 => ThunderscopeBandwidth.Bw20M,
            _ => ThunderscopeBandwidth.BwFull
        };

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
        acquisitionConfig.AdcChannelMode = channelCount switch
        {
            1 => AdcChannelMode.Single,
            2 => AdcChannelMode.Dual,
            _ => AdcChannelMode.Quad
        };
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

    public FrontendCalibration GetFrontendCalibration(int channelIndex)
    {
        CheckOpen();

        var afeCalibration = FrontendCalibration.Default(channelIndex);
        var tsCal = new Interop.tsFrontendCalibration_t();

        if (channelIndex >= 4 || channelIndex < 0)
            throw new ThunderscopeException($"Invalid Channel Index {channelIndex}");

        var retVal = Interop.GetAFECalibration(tsHandle, (uint)channelIndex, out tsCal);

        if (retVal < 0)
            throw new ThunderscopeException($"Failed to get libtslitex AFE{channelIndex} Calibration ({GetLibraryReturnString(retVal)})");

        for (int i = 0; i < 11; i++)
        {
            afeCalibration.Path[i].PgaPreampGain = PgaPreampGain.High;
            afeCalibration.Path[i].PgaLadder = (byte)i;
            afeCalibration.Path[i].TrimDPot = (byte)tsCal.highPgaPathCal[i].trimDPot;
            afeCalibration.Path[i].TrimDacScale = tsCal.highPgaPathCal[i].trimOffsetDacScale;
            afeCalibration.Path[i].TrimDacZeroC = tsCal.highPgaPathCal[i].trimOffsetDacZeroC;
            afeCalibration.Path[i].TrimDacZeroM = tsCal.highPgaPathCal[i].trimOffsetDacZeroM;
            afeCalibration.Path[i].BufferInputVpp = tsCal.highPgaPathCal[i].bufferInputVpp;

            afeCalibration.Path[11 + i].PgaPreampGain = PgaPreampGain.Low;
            afeCalibration.Path[11 + i].PgaLadder = (byte)i;
            afeCalibration.Path[11 + i].TrimDPot = (byte)tsCal.lowPgaPathCal[i].trimDPot;
            afeCalibration.Path[11 + i].TrimDacScale = tsCal.lowPgaPathCal[i].trimOffsetDacScale;
            afeCalibration.Path[11 + i].TrimDacZeroC = tsCal.lowPgaPathCal[i].trimOffsetDacZeroC;
            afeCalibration.Path[11 + i].TrimDacZeroM = tsCal.lowPgaPathCal[i].trimOffsetDacZeroM;
            afeCalibration.Path[11 + i].BufferInputVpp = tsCal.lowPgaPathCal[i].bufferInputVpp;
        }
        afeCalibration.AttenuatorScale = tsCal.attenuatorScale;
        return afeCalibration;
    }

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
        SetSampleMode(sampleRateHz, cachedSampleResolution);
    }

    public void SetResolution(AdcResolution resolution)
    {
        uint sampleRateHz = cachedSampleRateHz;
        if (resolution == AdcResolution.TwelveBit && sampleRateHz > 660_000_000)
            sampleRateHz = 660_000_000;

        SetSampleMode(sampleRateHz, resolution);
    }

    public void SetChannelFrontend(int channelIndex, ThunderscopeChannelFrontend channel)
    {
        CheckOpen();

        var tsChannel = new Interop.tsChannelParam_t();
        var retVal = Interop.GetChannelConfig(tsHandle, (uint)channelIndex, out tsChannel);

        if (retVal < 0)
            throw new ThunderscopeException($"Failed to get channel {channelIndex} config ({GetLibraryReturnString(retVal)})");

        tsChannel.volt_scale_uV = (uint)(channel.RequestedVoltFullScale * 1000000);
        tsChannel.volt_offset_uV = (int)(channel.RequestedVoltOffset * 1000000);
        tsChannel.coupling = (channel.Coupling == ThunderscopeCoupling.DC) ? (byte)0 : (byte)1;
        tsChannel.term = (channel.RequestedTermination == ThunderscopeTermination.OneMegaohm) ? (byte)0 : (byte)1;
        tsChannel.bandwidth = channel.Bandwidth switch
        {
            ThunderscopeBandwidth.BwFull => 900,
            ThunderscopeBandwidth.Bw750M => 750,
            ThunderscopeBandwidth.Bw650M => 650,
            ThunderscopeBandwidth.Bw350M => 350,
            ThunderscopeBandwidth.Bw200M => 200,
            ThunderscopeBandwidth.Bw100M => 100,
            ThunderscopeBandwidth.Bw20M => 20,
            _ => throw new NotImplementedException()
        };

        logger.LogInformation($"Configure channel {channelIndex}: scale {tsChannel.volt_scale_uV}uVpp, offset {tsChannel.volt_offset_uV}uV, term {tsChannel.term}");

        retVal = Interop.SetChannelConfig(tsHandle, (uint)channelIndex, in tsChannel);

        // libtslitex will return an error code if the requested channel configuration is not valid
        // so consumers of libtslitex have the luxury of knowing immediately if there is an issue,
        // whereas the SCPI API should not return error codes for commands so fall back to safe configurations if possible.

        if (retVal < 0 && channel.RequestedTermination == ThunderscopeTermination.FiftyOhm)
        {
            logger.LogWarning($"Failed to set channel {channelIndex} configuration; retrying with 1M termination ({GetLibraryReturnString(retVal)})");
            tsChannel.term = 0;
            retVal = Interop.SetChannelConfig(tsHandle, (uint)channelIndex, in tsChannel);
        }

        // This fallback isn't elegant, future improvements could calculate the best effort configuration that gets close to requested configuration
        if (retVal < 0)
        {
            logger.LogWarning($"Failed to set channel {channelIndex} configuration; retrying with 40V range and 0V offset ({GetLibraryReturnString(retVal)})");
            tsChannel.volt_scale_uV = 40_000_000;
            tsChannel.volt_offset_uV = 0;
            retVal = Interop.SetChannelConfig(tsHandle, (uint)channelIndex, in tsChannel);
        }

        if (retVal < 0)
            logger.LogCritical($"Failed to set channel {channelIndex} configuration ({GetLibraryReturnString(retVal)})");

        channelFrontend[channelIndex].RequestedVoltFullScale = channel.RequestedVoltFullScale;
        channelFrontend[channelIndex].RequestedVoltOffset = channel.RequestedVoltOffset;
        channelFrontend[channelIndex].RequestedTermination = channel.RequestedTermination;
        channelFrontend[channelIndex].Bandwidth = channel.Bandwidth;
        channelFrontend[channelIndex].Coupling = channel.Coupling;

        RefreshFrontendValues();

        channelManualOverride[channelIndex] = false;            // SetChannelManualControl sets to true, so immediately set to false
    }

    private void RefreshFrontendValues()
    {
        CheckOpen();
        
        for (int chIdx = 0; chIdx < 4; chIdx++)
        {
            Interop.tsChannelParam_t tsChannel;
            var retVal = Interop.GetChannelConfig(tsHandle, (uint)chIdx, out tsChannel);

            if (retVal < 0)
                throw new ThunderscopeException($"Failed to get channel {chIdx} configuration ({GetLibraryReturnString(retVal)})");

            channelFrontend[chIdx].ActualTermination = (tsChannel.term == 0) ? ThunderscopeTermination.OneMegaohm : ThunderscopeTermination.FiftyOhm;
            channelFrontend[chIdx].ActualVoltFullScale = tsChannel.volt_scale_uV / 1000000.0;
            channelFrontend[chIdx].ActualVoltOffset = tsChannel.volt_offset_uV / 1000000.0;
            
            logger.LogInformation($"Refresh channel {chIdx}: Req scale {channelFrontend[chIdx].RequestedVoltFullScale}Vpp, Act scale {channelFrontend[chIdx].ActualVoltFullScale}Vpp");
        }
    }

    public void SetAdcBranchGainManualControl(byte[] branchGain)
    {
        CheckOpen();

        var tsCal = new byte[8];
        tsCal[0] = branchGain[0];
        tsCal[1] = branchGain[1];
        tsCal[2] = branchGain[2];
        tsCal[3] = branchGain[3];
        tsCal[4] = branchGain[4];
        tsCal[5] = branchGain[5];
        tsCal[6] = branchGain[6];
        tsCal[7] = branchGain[7];

        Interop.SetAdcManualFineGain(tsHandle, in tsCal);
    }

    public void SetFrontendCalibration(int channelIndex, FrontendCalibration channelCalibration)
    {
        CheckOpen();

        if (channelIndex >= 4 || channelIndex < 0)
            throw new ThunderscopeException($"Invalid Channel Index {channelIndex}");

        var tsCal = new Interop.tsFrontendCalibration_t();

        foreach (var path in channelCalibration.Path)
        {
            if (path.PgaPreampGain == PgaPreampGain.High)
            {
                tsCal.highPgaPathCal[path.PgaLadder].trimDPot = path.TrimDPot;
                tsCal.highPgaPathCal[path.PgaLadder].trimOffsetDacZeroC = path.TrimDacZeroC;
                tsCal.highPgaPathCal[path.PgaLadder].trimOffsetDacZeroM = path.TrimDacZeroM;
                tsCal.highPgaPathCal[path.PgaLadder].trimOffsetDacScale = path.TrimDacScale;
                tsCal.highPgaPathCal[path.PgaLadder].bufferInputVpp = path.BufferInputVpp;
            }
            else
            {
                tsCal.lowPgaPathCal[path.PgaLadder].trimDPot = path.TrimDPot;
                tsCal.lowPgaPathCal[path.PgaLadder].trimOffsetDacZeroC = path.TrimDacZeroC;
                tsCal.lowPgaPathCal[path.PgaLadder].trimOffsetDacZeroM = path.TrimDacZeroM;
                tsCal.lowPgaPathCal[path.PgaLadder].trimOffsetDacScale = path.TrimDacScale;
                tsCal.lowPgaPathCal[path.PgaLadder].bufferInputVpp = path.BufferInputVpp;
            }
        }

        tsCal.attenuatorScale = channelCalibration.AttenuatorScale;

        var retVal = Interop.SetAfeCalibration(tsHandle, (uint)channelIndex, in tsCal);
        if (retVal < 0)
            throw new ThunderscopeException($"Failed to set libtslitex AFE{channelIndex} Calibration ({GetLibraryReturnString(retVal)})");

        RefreshFrontendValues();

    }

    public void SetAdcCalibration(AdcCalibration adcCalibration)
    {
        CheckOpen();

        var tsAdcCal = new Interop.tsAdcCalibration_t();
        for (int i = 0; i < adcCalibration.LoadScale.Length; i++)
        {
            tsAdcCal.loadCal[i].channels = (uint)adcCalibration.LoadScale[i].Channel.Aggregate(0, (current, chan) => 1 << chan);
            for (int j = 0; j < adcCalibration.LoadScale[i].RateScale.Length; j++)
            {
                tsAdcCal.loadCal[i].conf[j].rate = adcCalibration.LoadScale[i].RateScale[j].Rate;
                for (int k = 0; k < adcCalibration.LoadScale[i].RateScale[j].Scale.Length; k++)
                    tsAdcCal.loadCal[i].conf[j].scale[k] = adcCalibration.LoadScale[i].RateScale[j].Scale[k];
            }
        }
        for (int i = 0; i < adcCalibration.BranchGain.Length; i++)
        {
            tsAdcCal.branchFineGain[i].channels = (uint)adcCalibration.BranchGain[i].Channel.Aggregate(0, (current, chan) => 1 << chan);
            for (int j = 0; j < adcCalibration.BranchGain[i].RateGain.Length; j++)
            {
                tsAdcCal.branchFineGain[i].conf[j].rate = adcCalibration.BranchGain[i].RateGain[j].Rate;
                for (int k = 0; k < adcCalibration.BranchGain[i].RateGain[j].Gain.Length; k++)
                    tsAdcCal.branchFineGain[i].conf[j].gain[k] = (byte)adcCalibration.BranchGain[i].RateGain[j].Gain[k];
            }
        }
        RefreshFrontendValues();
    }

    public AdcCalibration GetAdcCalibration()
    {
        var adcCal = AdcCalibration.Default();
        var tsAdcCal = new Interop.tsAdcCalibration_t();
        if (Interop.GetAdcCalibration(tsHandle, out tsAdcCal) == 0)
        {
            // Convert Load Scale calibration
            for (int load = 0; load < 11; load++)
            {
                var channelCount = 0;
                for (int i = 0; i < 4; i++)
                {
                    if ((tsAdcCal.loadCal[load].channels & (1 << i)) != 0)
                    {
                        adcCal.LoadScale[load].Channel[channelCount] = i;
                        channelCount++;
                    }
                }
                for (int rateIdx = 0; rateIdx < 8; rateIdx++)
                {
                    adcCal.LoadScale[load].RateScale[rateIdx].Rate = tsAdcCal.loadCal[load].conf[rateIdx].rate;
                    for (int i = 0; i < channelCount; i++)
                    {
                        adcCal.LoadScale[load].RateScale[rateIdx].Scale[i] = tsAdcCal.loadCal[load].conf[rateIdx].scale[i];
                    }
                }
            }
            // Convert Branch Fine Gain calibration
            for (int gain = 0; gain < 11; gain++)
            {
                var channelCount = 0;
                for (int i = 0; i < 4; i++)
                {
                    if ((tsAdcCal.branchFineGain[gain].channels & (1 << i)) != 0)
                    {
                        adcCal.BranchGain[gain].Channel[channelCount] = i;
                        channelCount++;
                    }
                }
                for (int rateIdx = 0; rateIdx < 8; rateIdx++)
                {
                    adcCal.BranchGain[gain].RateGain[rateIdx].Rate = tsAdcCal.branchFineGain[gain].conf[rateIdx].rate;
                    for (int i = 0; i < 8; i++)
                    {
                        adcCal.BranchGain[gain].RateGain[rateIdx].Gain[i] = tsAdcCal.branchFineGain[gain].conf[rateIdx].gain[i];
                    }
                }
            }
        }

        return adcCal;
    }

    /// <summary>
    /// Intended for use by testbench/calibration routines that use SetChannelManualControl
    /// </summary>
    public void SetChannelEnable(int channelIndex, bool enabled)
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

        if (restart)
            Start();

        GetAcquisitionConfig();     // Update cachedAdcChannelMode
        RefreshFrontendValues();
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

    public int FactoryDataRead(uint tag, Span<byte> buffer)
    {
        CheckOpen();

        unsafe
        {
            fixed (byte* bufferP = buffer)
            {
                var retVal = Interop.FactoryDataRead(tsHandle, tag, bufferP, (uint)buffer.Length);
                if (retVal < 0)
                    throw new ThunderscopeException($"Failed to read factory data ({GetLibraryReturnString(retVal)})");
                return retVal;
            }
        }
    }

    private void UpdateFrontends()
    {
        for (int channelIndex = 0; channelIndex < 4; channelIndex++)
        {
            // Update all frontends that aren't under manual override, even for disabled channels (so relays actuate when expected)
            if (!channelManualOverride[channelIndex])
            {
                SetChannelFrontend(channelIndex, channelFrontend[channelIndex]);
            }
        }
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

    private void SetSampleMode(ulong sampleRateHz, AdcResolution resolution)
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

        if (retVal == -2)
            logger.LogTrace($"Failed to set sample rate ({sampleRateHz}): {GetLibraryReturnString(retVal)}");
        else if (retVal < 0)
            throw new ThunderscopeException($"Error trying to set sample rate {sampleRateHz} ({GetLibraryReturnString(retVal)})");

        RefreshFrontendValues();

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
                        UpdateFrontends();
                        lastFrontendUpdateTemp = currentTemp;
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
