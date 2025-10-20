using Microsoft.Extensions.Logging;
using NetCoreServer;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TS.NET.Engine;

class ScpiServer : TcpServer, IThread
{
    private readonly ILogger logger;
    private readonly ThunderscopeSettings settings;
    private readonly BlockingRequestResponse<HardwareRequestDto, HardwareResponseDto> hardwareControl;
    private readonly BlockingRequestResponse<ProcessingRequestDto, ProcessingResponseDto> processingControl;

    public ScpiServer(ILogger logger,
        ThunderscopeSettings settings,
        IPAddress address,
        int port,
        BlockingRequestResponse<HardwareRequestDto, HardwareResponseDto> hardwareControl,
        BlockingRequestResponse<ProcessingRequestDto, ProcessingResponseDto> processingControl) : base(address, port)
    {
        this.logger = logger;
        this.settings = settings;
        this.hardwareControl = hardwareControl;
        this.processingControl = processingControl;
        logger.LogDebug("Started");
    }

    protected override TcpSession CreateSession()
    {
        return new ScpiSession(this, logger, settings, hardwareControl, processingControl);
    }

    protected override void OnError(SocketError error)
    {
        logger.LogDebug($"SCPI server caught an error with code {error}");
    }

    public void Start(SemaphoreSlim startSemaphore)
    {
        base.Start();
        startSemaphore.Release();
    }

    public new void Stop()
    {
        base.Stop();
    }
}

internal class ScpiSession : TcpSession
{
    private readonly ILogger logger;
    private readonly BlockingRequestResponse<HardwareRequestDto, HardwareResponseDto> hardwareControl;
    private readonly BlockingRequestResponse<ProcessingRequestDto, ProcessingResponseDto> processingControl;
    private readonly ThunderscopeSettings settings;

    public ScpiSession(
        TcpServer server,
        ILogger logger,
        ThunderscopeSettings settings,
        BlockingRequestResponse<HardwareRequestDto, HardwareResponseDto> hardwareControl,
        BlockingRequestResponse<ProcessingRequestDto, ProcessingResponseDto> processingControl) : base(server)
    {
        this.logger = logger;
        this.settings = settings;
        this.hardwareControl = hardwareControl;
        this.processingControl = processingControl;
    }

    protected override void OnConnected()
    {
        logger.LogDebug($"SCPI session with ID {Id} connected!");
        //SendAsync("Hello!");
    }

    protected override void OnDisconnected()
    {
        logger.LogDebug($"SCPI session with ID {Id} disconnected!");
    }

    protected override void OnReceived(byte[] buffer, long offset, long size)
    {
        string messageStream = Encoding.UTF8.GetString(buffer, (int)offset, (int)size);
        var messages = messageStream.Split('\n');
        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message)) { continue; }

            string? response = ProcessSCPICommand(logger, settings, hardwareControl, processingControl, message.Trim());

            if (response != null)
            {
                logger.LogDebug("SCPI response: '{String}'", response.Trim());
                Send(response);
            }
        }
    }

    protected override void OnError(SocketError error)
    {
        logger.LogDebug($"SCPI session caught an error with code {error}");
    }

    public static string? ProcessSCPICommand(
        ILogger logger,
        ThunderscopeSettings settings,
        BlockingRequestResponse<HardwareRequestDto, HardwareResponseDto> hardwareControl,
        BlockingRequestResponse<ProcessingRequestDto, ProcessingResponseDto> processingControl,
        string message)
    {
        const string InvalidParameters = "One or more invalid parameters";

        string? argument = null;
        string? subject = null;
        string command = message;
        bool isQuery = false;

        int? GetChannelIndex(string arg)
        {
            if (!char.IsDigit(arg[^1]))
            {
                logger.LogWarning("Channel parameter not valid");
                return null;
            }
            int channelNumber = arg[^1] - '0';
            if (channelNumber < 1 || channelNumber > 4)
            {
                logger.LogWarning("Channel parameter out of range, allowable values are 1 - 4");
                return null;
            }
            return channelNumber - 1;
        }

        ThunderscopeBandwidth? GetBandwidth(string arg)
        {
            ThunderscopeBandwidth? thunderscopeBandwidth = arg switch
            {
                "FULL" => ThunderscopeBandwidth.BwFull,
                "750M" => ThunderscopeBandwidth.Bw750M,
                "650M" => ThunderscopeBandwidth.Bw650M,
                "350M" => ThunderscopeBandwidth.Bw350M,
                "200M" => ThunderscopeBandwidth.Bw200M,
                "100M" => ThunderscopeBandwidth.Bw100M,
                "20M" => ThunderscopeBandwidth.Bw20M,
                _ => null
            };
            if (thunderscopeBandwidth == null)
            {
                logger.LogWarning("Bandwidth parameter not recognised");
                return null;
            }
            return thunderscopeBandwidth;
        }

        ThunderscopeTermination? GetTermination(string arg)
        {
            ThunderscopeTermination? thunderscopeTermination = arg switch
            {
                "1M" => ThunderscopeTermination.OneMegaohm,
                "50" => ThunderscopeTermination.FiftyOhm,
                _ => null
            };
            if (thunderscopeTermination == null)
            {
                logger.LogWarning("Termination parameter not recognised");
                return null;
            }
            return thunderscopeTermination;
        }

        logger.LogDebug($"SCPI request: {message}");

        if (message.Contains(" "))
        {
            int index = message.IndexOf(" ");
            argument = message.Substring(index + 1);
            command = message.Substring(0, index);
        }
        else if (command.Contains("?"))
        {
            isQuery = true;
            //command = message.Substring(0, message.Length - 1);
        }

        if (command.StartsWith(":"))
        {
            command = command.Substring(1);
        }

        if (command.Contains(":"))
        {
            int index = command.IndexOf(":");
            subject = command.Substring(0, index);
            command = command.Substring(index + 1);
        }
        // logger.LogDebug("o:'{String}', q:{bool}, s:'{String?}', c:'{String?}', a:'{String?}'", fullCommand, isQuery, subject, command, argument);

        if (!isQuery)
        {
            switch (subject)
            {
                case null:
                    {
                        switch (command)
                        {
                            case "START":   //Obsolete
                            case "RUN":
                                processingControl.Request.Writer.Write(new ProcessingRunDto());
                                logger.LogDebug($"{nameof(ProcessingRunDto)} sent");
                                return null;
                            case "STOP":
                                processingControl.Request.Writer.Write(new ProcessingStopDto());
                                logger.LogDebug($"{nameof(ProcessingStopDto)} sent");
                                return null;
                            case "FORCE":
                                processingControl.Request.Writer.Write(new ProcessingForceDto());
                                logger.LogDebug($"{nameof(ProcessingForceDto)} sent");
                                return null;
                            case "SINGLE":
                                processingControl.Request.Writer.Write(new ProcessingSetModeDto(Mode.Single));
                                logger.LogDebug($"{nameof(ProcessingSetModeDto)} sent");
                                return null;
                            case "NORMAL":
                                processingControl.Request.Writer.Write(new ProcessingSetModeDto(Mode.Normal));
                                logger.LogDebug($"{nameof(ProcessingSetModeDto)} sent");
                                return null;
                            case "AUTO":
                                processingControl.Request.Writer.Write(new ProcessingSetModeDto(Mode.Auto));
                                logger.LogDebug($"{nameof(ProcessingSetModeDto)} sent");
                                return null;
                            case "STREAM":
                                processingControl.Request.Writer.Write(new ProcessingSetModeDto(Mode.Stream));
                                logger.LogDebug($"{nameof(ProcessingSetModeDto)} sent");
                                return null;
                        }
                        break;
                    }
                case var _ when subject.StartsWith("ACQ"):
                    {
                        // ACQuisition:
                        // ACQ
                        switch (command)
                        {
                            case var _ when command.StartsWith("RATE") && argument != null:
                                {
                                    ulong rate = Convert.ToUInt64(argument);
                                    hardwareControl.Request.Writer.Write(new HardwareSetRateRequest(rate));
                                    logger.LogDebug($"{nameof(HardwareSetRateRequest)} sent with argument: {rate}");
                                    return null;
                                }
                            case var _ when command.StartsWith("DEPTH") && argument != null:
                                {
                                    var depth = Convert.ToInt32(argument);
                                    processingControl.Request.Writer.Write(new ProcessingSetDepthDto(depth));
                                    logger.LogDebug($"{nameof(ProcessingSetDepthDto)} sent with argument: {depth}");
                                    return null;
                                }
                            case var _ when command.StartsWith("RES") && argument != null:
                                {
                                    var resolution = Convert.ToInt32(argument) switch { 8 => AdcResolution.EightBit, 12 => AdcResolution.TwelveBit, _ => AdcResolution.EightBit };
                                    hardwareControl.Request.Writer.Write(new HardwareSetResolutionRequest(resolution));
                                    logger.LogDebug($"{nameof(HardwareSetResolutionRequest)} sent with argument: {resolution}");
                                    return null;
                                }

                        }
                        break;
                    }
                //case var _ when subject.StartsWith("TIM"):
                //    {
                //        // TIMebase:POSition <arg> instead of TRIGger:DELay <arg>?
                //    }
                case var _ when subject.StartsWith("TRIG"):
                    {
                        // TRIGger:
                        // TRIG:
                        switch (command)
                        {
                            case var _ when command.StartsWith("SOU") && argument != null:
                                {
                                    // TRIGger:SOUrce <arg>
                                    // TRIG:SOU <arg>
                                    if (!char.IsDigit(argument[^1]) && argument != "NONE")
                                    {
                                        logger.LogWarning($"Trigger source parameter not valid");
                                        return null;
                                    }
                                    var triggerChannel = TriggerChannel.None;
                                    if (argument != "NONE")
                                    {
                                        int source = Convert.ToInt32(argument.ToArray()[^1]) - '0';
                                        if (source < 1 || source > 4)
                                            source = 1;
                                        triggerChannel = (TriggerChannel)source;
                                    }
                                    logger.LogDebug($"Set trigger source to {triggerChannel}");
                                    processingControl.Request.Writer.Write(new ProcessingSetTriggerSourceDto(triggerChannel));
                                    return null;
                                }
                            case var _ when command.StartsWith("TYPE") && argument != null:
                                {
                                    // TRIGger:TYPE <arg>
                                    // TRIG:TYPE <arg>
                                    TriggerType? triggerType = argument.ToUpper() switch
                                    {
                                        "EDGE" => TriggerType.Edge,
                                        "WINDOW" => TriggerType.Window,
                                        "RUNT" => TriggerType.Runt,
                                        "WIDTH" => TriggerType.Width,
                                        "INTERVAL" => TriggerType.Interval,
                                        "BURST" => TriggerType.Burst,
                                        "DROPOUT" => TriggerType.Dropout,
                                        "SLEWRATE" => TriggerType.SlewRate,
                                        _ => null
                                    };

                                    if (triggerType == null)
                                    {
                                        logger.LogWarning("Trigger type parameter not recognised: {Argument}. Valid values: EDGE, WINDOW, RUNT, WIDTH, INTERVAL, BURST, DROPOUT, SLEWRATE", argument);
                                        return null;
                                    }

                                    logger.LogDebug($"Set trigger type to {triggerType}");
                                    processingControl.Request.Writer.Write(new ProcessingSetTriggerTypeDto(triggerType.Value));
                                    return null;
                                }
                            case var _ when command.StartsWith("DEL") && argument != null:
                                {
                                    // TRIGger:DELay <arg>
                                    // TRIG:DEL <arg>
                                    long delay = Convert.ToInt64(argument);
                                    logger.LogDebug($"Set trigger delay to {delay}fs");
                                    processingControl.Request.Writer.Write(new ProcessingSetTriggerDelayDto((ulong)delay));
                                    return null;
                                }
                            case var _ when command.StartsWith("HOLD") && argument != null:
                                {
                                    // TRIGger:HOLDoff <arg>
                                    // TRIG:HOLD <arg>
                                    long holdoff = Convert.ToInt64(argument);
                                    logger.LogDebug($"Set trigger holdoff to {holdoff}fs");
                                    processingControl.Request.Writer.Write(new ProcessingSetTriggerHoldoffDto((ulong)holdoff));
                                    return null;
                                }
                            case var _ when command.StartsWith("INTER") && argument != null:
                                {
                                    // TRIGger:INTERpolation <arg>
                                    // TRIG:INTER <arg>
                                    bool enabled = argument.ToLower() switch
                                    {
                                        "true" => true,
                                        "1" => true,
                                        "false" => false,
                                        "0" => false,
                                        _ => true       // Default to true
                                    };
                                    logger.LogDebug($"Set trigger interpolation to {enabled}V");
                                    processingControl.Request.Writer.Write(new ProcessingSetTriggerInterpolationDto(enabled));
                                    return null;
                                }
                            case var _ when command.StartsWith("EDGE:LEV") && argument != null:
                                {
                                    // TRIGger:EDGE:LEVel <arg>
                                    // TRIG:EDGE:LEV <arg>
                                    double level = Convert.ToDouble(argument);
                                    logger.LogDebug($"Set trigger level to {level}V");
                                    processingControl.Request.Writer.Write(new ProcessingSetEdgeTriggerLevelDto(level));
                                    return null;
                                }
                            case var _ when command.StartsWith("EDGE:DIR") && argument != null:
                                {
                                    // TRIGger:EDGE:DIRection <arg>
                                    // TRIG:EDGE:DIR <arg>
                                    string dir = argument ?? throw new NullReferenceException();
                                    logger.LogDebug($"Set [edge] trigger direction to {dir}");
                                    var type = dir.ToUpper() switch
                                    {
                                        "RISING" => EdgeDirection.Rising,
                                        "FALLING" => EdgeDirection.Falling,
                                        "ANY" => EdgeDirection.Any,
                                        _ => throw new NotImplementedException()
                                    };
                                    processingControl.Request.Writer.Write(new ProcessingSetEdgeTriggerDirectionDto(type));
                                    return null;
                                }
                        }
                        break;
                    }
                case var _ when subject.StartsWith("CHAN") && char.IsDigit(subject[^1]):
                    {
                        // CHANnel1:
                        // CHAN1:
                        if (GetChannelIndex(subject) is not int channelIndex)
                            return null;
                        switch (command)
                        {
                            case "ON" or "OFF":
                                {
                                    hardwareControl.Request.Writer.Write(new HardwareSetEnabledRequest(channelIndex, command == "ON"));
                                    return null;
                                }
                            case var _ when command.StartsWith("BAND") && argument != null:
                                {
                                    // CHANnel1:BANDwidth <arg>
                                    // CHAN1:BAND <arg>
                                    if (GetBandwidth(argument) is not ThunderscopeBandwidth thunderscopeBandwidth)
                                        return null;
                                    hardwareControl.Request.Writer.Write(new HardwareSetBandwidthRequest(channelIndex, thunderscopeBandwidth));
                                    return null;
                                }
                            case var _ when command.StartsWith("COUP") && argument != null:
                                {
                                    // CHANnel1:COUPling <arg>
                                    // CHAN1:COUP <arg>
                                    ThunderscopeCoupling? thunderscopeCoupling = argument switch
                                    {
                                        "DC" => ThunderscopeCoupling.DC,
                                        "AC" => ThunderscopeCoupling.AC,
                                        _ => null
                                    };
                                    if (thunderscopeCoupling == null)
                                    {
                                        logger.LogWarning("Coupling parameter not recognised");
                                        return null;
                                    }
                                    hardwareControl.Request.Writer.Write(new HardwareSetCouplingRequest(channelIndex, (ThunderscopeCoupling)thunderscopeCoupling));
                                    return null;
                                }
                            case var _ when command.StartsWith("TERM") && argument != null:
                                {
                                    // CHANnel1:TERMination <arg>
                                    // CHAN1:TERM <arg>
                                    if (GetTermination(argument) is not ThunderscopeTermination thunderscopeTermination)
                                        return null;
                                    hardwareControl.Request.Writer.Write(new HardwareSetTerminationRequest(channelIndex, thunderscopeTermination));
                                    return null;
                                }
                            case var _ when command.StartsWith("OFFS") && argument != null:
                                {
                                    // CHANnel1:OFFSet <arg>
                                    // CHAN1:OFFS <arg>
                                    double offset = Convert.ToDouble(argument);
                                    offset = Math.Clamp(offset, -50, 50);     // Change to final values later
                                    hardwareControl.Request.Writer.Write(new HardwareSetVoltOffsetRequest(channelIndex, offset));
                                    return null;
                                }
                            case var _ when command.StartsWith("RANG") && argument != null:
                                {
                                    double range = Convert.ToDouble(argument);
                                    range = Math.Clamp(range, -50, 50);       // Change to final values later
                                    hardwareControl.Request.Writer.Write(new HardwareSetVoltFullScaleRequest(channelIndex, range));
                                    return null;
                                }
                            default:
                                break;  // Will log a warning at the end of the handler
                        }
                        break;
                    }
                // Change this to a better name later
                case var _ when subject.StartsWith("PRO"):
                    {
                        // :PROcessing
                        // :PRO
                        switch (command)
                        {
                            // :FILter
                            // :FIL
                            // :PRO:FIL BOXCAR 2
                            //    Boxcar average by 2, this is the same as a FIR filter of N taps with uniform weights, giving a Sinc1 response. Allowable N is 2-1000000.
                            // :PRO:FIL FIR GAUSSIAN <taps>
                            //    This is a FIR filter with a gaussian response.
                            case var _ when command.StartsWith("FIL") && argument != null:
                                {
                                    try
                                    {
                                        var args = argument.Split(' ');
                                        if (args[0] == "BOXCAR")
                                        {
                                            var length = int.Parse(args[1]);

                                        }
                                    }
                                    catch
                                    {
                                        logger.LogWarning(InvalidParameters);
                                        return null;
                                    }
                                    break;
                                }
                        }
                        break;
                    }
                case var _ when subject.StartsWith("CAL"):
                    {
                        // :CALibration
                        // :CAL
                        switch (command)
                        {
                            // :FRONTEND
                            case var _ when command.StartsWith("FRONTEND") && argument != null:
                                {
                                    // Channel Coupling Termination Attenuator DAC DPOT PgaLadderAttenuation PgaHighGain PgaFilter
                                    // CAL:FRONTEND CHAN1 DC 1M 0 2147 4 0 1 FULL
                                    //    - highest gain configuration with attenuator off and maximum offset adjustment range. Adjust 2147 until midscale.
                                    // CAL:FRONTEND CHAN1 DC 1M 1 2147 4 0 1 FULL
                                    // 4 = 1563.5 ohms
                                    try
                                    {
                                        var args = argument.Split(' ');
                                        if (GetChannelIndex(args[0]) is not int channelIndex)
                                            return null;
                                        var channel = new ThunderscopeChannelFrontendManualControl();
                                        ThunderscopeCoupling? thunderscopeCoupling = args[1] switch
                                        {
                                            "DC" => ThunderscopeCoupling.DC,
                                            "AC" => ThunderscopeCoupling.AC,
                                            _ => null
                                        };
                                        if (thunderscopeCoupling == null)
                                        {
                                            logger.LogWarning("Coupling parameter not recognised");
                                            return null;
                                        }
                                        channel.Coupling = (ThunderscopeCoupling)thunderscopeCoupling;

                                        if (GetTermination(args[2]) is not ThunderscopeTermination thunderscopeTermination)
                                            return null;
                                        channel.Termination = thunderscopeTermination;

                                        channel.Attenuator = byte.Parse(args[3]);
                                        channel.DAC = ushort.Parse(args[4]);
                                        channel.DPOT = byte.Parse(args[5]);

                                        channel.PgaLadderAttenuation = byte.Parse(args[6]);   // 0 to 10, 0/-2/-4/-6/-8/-10/-12/-14/-16/-18/-20
                                        channel.PgaHighGain = byte.Parse(args[7]);            // 0 = LG, 1 = HG

                                        if (GetBandwidth(args[8]) is not ThunderscopeBandwidth thunderscopeBandwidth)
                                            return null;
                                        channel.PgaFilter = thunderscopeBandwidth;

                                        hardwareControl.Request.Writer.Write(new HardwareSetChannelManualControlRequest(channelIndex, channel));
                                    }
                                    catch
                                    {
                                        logger.LogWarning(InvalidParameters);
                                        return null;
                                    }
                                    return null;
                                }
                            case var _ when command.StartsWith("ADC") && argument != null:
                                {
                                    var args = argument.Split(' ');
                                    if (args.Length != 8)
                                    {
                                        logger.LogWarning("Parameter count should be 8 values");
                                        return null;
                                    }
                                    var adcCal = new ThunderscopeAdcCalibration();
                                    try
                                    {
                                        adcCal.FineGainBranch1 = Convert.ToByte(args[0]);
                                        adcCal.FineGainBranch2 = Convert.ToByte(args[1]);
                                        adcCal.FineGainBranch3 = Convert.ToByte(args[2]);
                                        adcCal.FineGainBranch4 = Convert.ToByte(args[3]);
                                        adcCal.FineGainBranch5 = Convert.ToByte(args[4]);
                                        adcCal.FineGainBranch6 = Convert.ToByte(args[5]);
                                        adcCal.FineGainBranch7 = Convert.ToByte(args[6]);
                                        adcCal.FineGainBranch8 = Convert.ToByte(args[7]);
                                        hardwareControl.Request.Writer.Write(new HardwareSetAdcCalibrationRequest(adcCal));
                                    }
                                    catch
                                    {
                                        logger.LogWarning("Not able to parse values");
                                        return null;
                                    }
                                    return null;
                                }
                        }
                        break;
                    }
            }
        }
        else
        {
            if (subject == null)
            {
                switch (command)
                {
                    case "*IDN?":
                        return "EEVengers,ThunderScope,NO_SERIAL,NO_VERSION\n";
                    case "STATE?":
                        {
                            processingControl.Request.Writer.Write(new ProcessingGetStateRequest());
                            if (processingControl.Response.Reader.TryRead(out var response, 500))
                            {
                                switch (response)
                                {
                                    case ProcessingGetStateResponse processingGetStateResponse:
                                        return $"{(processingGetStateResponse.Run ? "RUN" : "STOP")}\n";
                                    default:
                                        logger.LogError($"STATE? - Invalid response from {nameof(processingControl.Response.Reader)}");
                                        break;
                                }
                            }
                            logger.LogError($"STATE? - No response from {nameof(processingControl.Response.Reader)}");
                            return "Error: No/bad response from channel.\n";
                        }
                    case "MODE?":
                        {
                            processingControl.Request.Writer.Write(new ProcessingGetModeRequest());
                            if (processingControl.Response.Reader.TryRead(out var response, 500))
                            {
                                switch (response)
                                {
                                    case ProcessingGetModeResponse processingGetModeResponse:
                                        return $"{processingGetModeResponse.Mode.ToString().ToUpper()}\n";
                                    default:
                                        logger.LogError($"MODE? - Invalid response from {nameof(processingControl.Response.Reader)}");
                                        break;
                                }
                            }
                            logger.LogError($"MODE? - No response from {nameof(processingControl.Response.Reader)}");
                            return "Error: No/bad response from channel.\n";
                        }
                }
            }
            else if (subject?.StartsWith("ACQ") == true)
            {
                // ACQuisition:
                // ACQ
                switch (command)
                {
                    case "RATES?":
                        return GetRates();
                    case "RATE?":
                        return GetRate();
                    case "DEPTHS?":
                        return GetDepths();
                    case "DEPTH?":
                        return GetDepth();
                    case var _ when command.StartsWith("RES"):
                        {
                            hardwareControl.Request.Writer.Write(new HardwareGetResolutionRequest());
                            if (hardwareControl.Response.Reader.TryRead(out var response, 500))
                            {
                                if (response is HardwareGetResolutionResponse hardwareGetResolutionResponse)
                                {
                                    switch (hardwareGetResolutionResponse.Resolution)
                                    {
                                        case AdcResolution.EightBit:
                                            return "8\n";
                                        case AdcResolution.TwelveBit:
                                            return "12\n";
                                        default:
                                            logger.LogError($"{subject}:RES? - Unhandled response from {nameof(hardwareControl.Response.Reader)}");
                                            break;
                                    }
                                }
                                else
                                {
                                    logger.LogError($"{subject}:RES? - Invalid response from {nameof(hardwareControl.Response.Reader)}");
                                }
                            }
                            else
                            {
                                logger.LogError($"{subject}:RES? - No response from {nameof(hardwareControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }

                }
            }
            else if (subject?.StartsWith("TRIG") == true)
            {
                // Trigger query commands
                while (processingControl.Response.Reader.TryRead(out var response, 10)) { }
                switch (command)
                {
                    case var _ when command.StartsWith("SOU"):
                        {
                            processingControl.Request.Writer.Write(new ProcessingGetTriggerSourceRequest());
                            if (processingControl.Response.Reader.TryRead(out var response, 500))
                            {
                                switch (response)
                                {
                                    case ProcessingGetTriggerSourceResponse triggerSourceResponse:
                                        return $"CHAN{(int)triggerSourceResponse.Channel}\n";
                                    default:
                                        logger.LogError($"TRIG:SOU? - Invalid response from {nameof(processingControl.Response.Reader)}");
                                        break;
                                }
                            }
                            else
                            {
                                logger.LogError($"TRIG:SOU? - No response from {nameof(processingControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }
                    case var _ when command.StartsWith("TYPE"):
                        {
                            processingControl.Request.Writer.Write(new ProcessingGetTriggerTypeRequest());
                            if (processingControl.Response.Reader.TryRead(out var response, 500))
                            {
                                switch (response)
                                {
                                    case ProcessingGetTriggerTypeResponse triggerTypeResponse:
                                        return $"{triggerTypeResponse.Type.ToString().ToUpper()}\n";
                                    default:
                                        logger.LogError($"TRIG:TYPE? - Invalid response from {nameof(processingControl.Response.Reader)}");
                                        break;
                                }
                            }
                            else
                            {
                                logger.LogError($"TRIG:TYPE? - No response from {nameof(processingControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }
                    case var _ when command.StartsWith("DEL"):
                        {
                            processingControl.Request.Writer.Write(new ProcessingGetTriggerDelayRequest());
                            if (processingControl.Response.Reader.TryRead(out var response, 500))
                            {
                                switch (response)
                                {
                                    case ProcessingGetTriggerDelayResponse triggerDelayResponse:
                                        return $"{triggerDelayResponse.Femtoseconds}\n";
                                    default:
                                        logger.LogError($"TRIG:DEL? - Invalid response from {nameof(processingControl.Response.Reader)}");
                                        break;
                                }
                            }
                            else
                            {
                                logger.LogError($"TRIG:DEL? - No response from {nameof(processingControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }
                    case var _ when command.StartsWith("HOLD"):
                        {
                            processingControl.Request.Writer.Write(new ProcessingGetTriggerHoldoffRequest());
                            if (processingControl.Response.Reader.TryRead(out var response, 500))
                            {
                                switch (response)
                                {
                                    case ProcessingGetTriggerHoldoffResponse triggerHoldoffResponse:
                                        return $"{triggerHoldoffResponse.Femtoseconds}\n";
                                    default:
                                        logger.LogError($"TRIG:HOLD? - Invalid response from {nameof(processingControl.Response.Reader)}");
                                        break;
                                }
                            }
                            else
                            {
                                logger.LogError($"TRIG:HOLD? - No response from {nameof(processingControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }
                    case var _ when command.StartsWith("INTER"):
                        {
                            processingControl.Request.Writer.Write(new ProcessingGetTriggerInterpolationRequest());
                            if (processingControl.Response.Reader.TryRead(out var response, 500))
                            {
                                switch (response)
                                {
                                    case ProcessingGetTriggerInterpolationResponse triggerInterpolationResponse:
                                        return $"{(triggerInterpolationResponse.Enabled ? "1" : "0")}\n";
                                    default:
                                        logger.LogError($"TRIG:INTER? - Invalid response from {nameof(processingControl.Response.Reader)}");
                                        break;
                                }
                            }
                            else
                            {
                                logger.LogError($"TRIG:INTER? - No response from {nameof(processingControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }
                    case var _ when command.StartsWith("EDGE:LEV"):
                        {
                            processingControl.Request.Writer.Write(new ProcessingGetEdgeTriggerLevelRequest());
                            if (processingControl.Response.Reader.TryRead(out var response, 500))
                            {
                                switch (response)
                                {
                                    case ProcessingGetEdgeTriggerLevelResponse triggerLevelResponse:
                                        return $"{triggerLevelResponse.LevelVolts:0.######}\n";
                                    default:
                                        logger.LogError($"TRIG:EDGE:LEV? - Invalid response from {nameof(processingControl.Response.Reader)}");
                                        break;
                                }
                            }
                            else
                            {
                                logger.LogError($"TRIG:EDGE:LEV? - No response from {nameof(processingControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }
                    case var _ when command.StartsWith("EDGE:DIR"):
                        {
                            processingControl.Request.Writer.Write(new ProcessingGetEdgeTriggerDirectionRequest());
                            if (processingControl.Response.Reader.TryRead(out var response, 500))
                            {
                                switch (response)
                                {
                                    case ProcessingGetEdgeTriggerDirectionResponse triggerDirectionResponse:
                                        return $"{triggerDirectionResponse.Direction.ToString().ToUpper()}\n";
                                    default:
                                        logger.LogError($"TRIG:EDGE:DIR? - Invalid response from {nameof(processingControl.Response.Reader)}");
                                        break;
                                }
                            }
                            else
                            {
                                logger.LogError($"TRIG:EDGE:DIR? - No response from {nameof(processingControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }
                }
            }
            else if (subject?.StartsWith("CHAN") == true)
            {
                // CHANnel1:
                // CHAN1:
                if (GetChannelIndex(subject) is not int channelIndex)
                    return null;

                while (hardwareControl.Response.Reader.TryRead(out var _, 10)) { }
                switch (command)
                {
                    case var _ when command.Equals("STATE?", StringComparison.OrdinalIgnoreCase):
                        {
                            hardwareControl.Request.Writer.Write(new HardwareGetEnabledRequest(channelIndex));
                            if (hardwareControl.Response.Reader.TryRead(out var response, 500))
                            {
                                if (response is HardwareGetEnabledResponse hardwareGetEnabledResponse)
                                {
                                    return hardwareGetEnabledResponse.Enabled ? "ON\n" : "OFF\n";
                                }
                                else
                                {
                                    logger.LogError($"{subject}:STATE? - Invalid response from {nameof(hardwareControl.Response.Reader)}");
                                }
                            }
                            else
                            {
                                logger.LogError($"{subject}:STATE? - No response from {nameof(hardwareControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }
                    case var _ when command.StartsWith("BAND", StringComparison.OrdinalIgnoreCase):
                        {
                            hardwareControl.Request.Writer.Write(new HardwareGetBandwidthRequest(channelIndex));
                            if (hardwareControl.Response.Reader.TryRead(out var response, 500))
                            {
                                if (response is HardwareGetBandwidthResponse hardwareGetBandwidthResponse)
                                {
                                    string bandwidth = hardwareGetBandwidthResponse.Bandwidth switch
                                    {
                                        ThunderscopeBandwidth.BwFull => "FULL",
                                        ThunderscopeBandwidth.Bw750M => "750M",
                                        ThunderscopeBandwidth.Bw650M => "650M",
                                        ThunderscopeBandwidth.Bw350M => "350M",
                                        ThunderscopeBandwidth.Bw200M => "200M",
                                        ThunderscopeBandwidth.Bw100M => "100M",
                                        ThunderscopeBandwidth.Bw20M => "20M",
                                        _ => hardwareGetBandwidthResponse.Bandwidth.ToString().ToUpper()
                                    };
                                    return bandwidth + "\n";
                                }
                                else
                                {
                                    logger.LogError($"{subject}:BAND? - Invalid response from {nameof(hardwareControl.Response.Reader)}");
                                }
                            }
                            else
                            {
                                logger.LogError($"{subject}:BAND? - No response from {nameof(hardwareControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }
                    case var _ when command.StartsWith("COUP", StringComparison.OrdinalIgnoreCase):
                        {
                            hardwareControl.Request.Writer.Write(new HardwareGetCouplingRequest(channelIndex));
                            if (hardwareControl.Response.Reader.TryRead(out var response, 500))
                            {
                                if (response is HardwareGetCouplingResponse hardwareGetCouplingResponse)
                                {
                                    string coupling = hardwareGetCouplingResponse.Coupling switch
                                    {
                                        ThunderscopeCoupling.DC => "DC",
                                        ThunderscopeCoupling.AC => "AC",
                                        _ => hardwareGetCouplingResponse.Coupling.ToString().ToUpper()
                                    };
                                    return coupling + "\n";
                                }
                                else
                                {
                                    logger.LogError($"{subject}:COUP? - Invalid response from {nameof(hardwareControl.Response.Reader)}");
                                }
                            }
                            else
                            {
                                logger.LogError($"{subject}:COUP? - No response from {nameof(hardwareControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }
                    case var _ when command.StartsWith("TERM", StringComparison.OrdinalIgnoreCase):
                        {
                            hardwareControl.Request.Writer.Write(new HardwareGetTerminationRequest(channelIndex));
                            if (hardwareControl.Response.Reader.TryRead(out var response, 500))
                            {
                                if (response is HardwareGetTerminationResponse hardwareGetTerminationResponse)
                                {
                                    string termination = hardwareGetTerminationResponse.Termination switch
                                    {
                                        ThunderscopeTermination.OneMegaohm => "1M",
                                        ThunderscopeTermination.FiftyOhm => "50",
                                        _ => hardwareGetTerminationResponse.Termination.ToString().ToUpper()
                                    };
                                    return termination + "\n";
                                }
                                else
                                {
                                    logger.LogError($"{subject}:TERM? - Invalid response from {nameof(hardwareControl.Response.Reader)}");
                                }
                            }
                            else
                            {
                                logger.LogError($"{subject}:TERM? - No response from {nameof(hardwareControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }
                    case var _ when command.StartsWith("OFFS", StringComparison.OrdinalIgnoreCase):
                        {
                            hardwareControl.Request.Writer.Write(new HardwareGetVoltOffsetRequest(channelIndex));
                            if (hardwareControl.Response.Reader.TryRead(out var response, 500))
                            {
                                if (response is HardwareGetVoltOffsetResponse hardwareGetVoltOffsetResponse)
                                    return $"{hardwareGetVoltOffsetResponse.RequestedVoltOffset:0.######}\n";
                                logger.LogError($"{subject}:OFFS? - Invalid response from {nameof(hardwareControl.Response.Reader)}");
                            }
                            else
                            {
                                logger.LogError($"{subject}:OFFS? - No response from {nameof(hardwareControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }
                    case var _ when command.StartsWith("RANG", StringComparison.OrdinalIgnoreCase):
                        {
                            hardwareControl.Request.Writer.Write(new HardwareGetVoltFullScaleRequest(channelIndex));
                            if (hardwareControl.Response.Reader.TryRead(out var response, 500))
                            {
                                if (response is HardwareGetVoltFullScaleResponse hardwareGetVoltFullScaleResponse)
                                {
                                    return $"{hardwareGetVoltFullScaleResponse.RequestedVoltFullScale:0.######}\n";
                                }
                                else
                                {
                                    logger.LogError($"{subject}:RANG? - Invalid response from {nameof(hardwareControl.Response.Reader)}");
                                }
                            }
                            else
                            {
                                logger.LogError($"{subject}:RANG? - No response from {nameof(hardwareControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }
                }
            }
        }

        logger.LogWarning("Unknown SCPI command: {String}", message);
        return null;

        string GetRates()
        {
            hardwareControl.Request.Writer.Write(new HardwareGetRatesRequest());
            if (hardwareControl.Response.Reader.TryRead(out var response, 500))
            {
                switch (response)
                {
                    case HardwareGetRatesResponse hardwareGetRatesResponse:
                        return $"{string.Join(",", hardwareGetRatesResponse.SampleRatesHz)},\n";
                    default:
                        logger.LogError($"RATES? - Invalid response from {nameof(hardwareControl.Response.Reader)}");
                        break;
                }
            }
            logger.LogError($"RATES? - No response from {nameof(hardwareControl.Response.Reader)}");
            return "Error: No/bad response from channel.\n";
        }

        string GetRate()
        {
            hardwareControl.Request.Writer.Write(new HardwareGetRateRequest());
            if (hardwareControl.Response.Reader.TryRead(out var response, 500))
            {
                switch (response)
                {
                    case HardwareGetRateResponse hardwareGetRateResponse:
                        return $"{hardwareGetRateResponse.SampleRateHz}\n";
                    default:
                        logger.LogError($"RATE? - Invalid response from {nameof(hardwareControl.Response.Reader)}");
                        break;
                }
            }
            logger.LogError($"RATE? - No response from {nameof(hardwareControl.Response.Reader)}");
            return "Error: No/bad response from channel.\n";
        }

        string GetDepths()
        {
            List<string> depths = [];
            long baseCount = 1000;
            while (true)
            {
                if (baseCount <= settings.MaxCaptureLength)
                    depths.Add($"{baseCount}");
                if (baseCount * 2 <= settings.MaxCaptureLength)
                    depths.Add($"{baseCount * 2}");
                if (baseCount * 5 <= settings.MaxCaptureLength)
                    depths.Add($"{baseCount * 5}");
                baseCount *= 10;
                if (baseCount > settings.MaxCaptureLength)
                    break;
            }
            // Perhaps take into account the sample rate to get 1ms/2ms/5ms/10ms/etc windows instead?
            return $"{string.Join(",", depths)},\n";
        }

        string GetDepth()
        {
            processingControl.Request.Writer.Write(new ProcessingGetDepthRequest());
            if (processingControl.Response.Reader.TryRead(out var response, 500))
            {
                switch (response)
                {
                    case ProcessingGetDepthResponse processingGetDepthResponse:
                        return $"{processingGetDepthResponse.Depth}\n";
                    default:
                        logger.LogError($"DEPTH? - Invalid response from {nameof(processingControl.Response.Reader)}");
                        break;
                }
            }
            logger.LogError($"DEPTH? - No response from {nameof(processingControl.Response.Reader)}");
            return "Error: No/bad response from channel.\n";
        }
    }
}
