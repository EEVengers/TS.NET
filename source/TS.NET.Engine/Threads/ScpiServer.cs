using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TS.NET.Engine;

internal class ScpiServer : IThread
{
    private readonly ILogger logger;
    private readonly ThunderscopeSettings settings;
    private readonly string thunderscopeSerial;
    private readonly IPAddress address;
    private readonly int port;
    private readonly BlockingRequestResponse<ProcessingRequestDto, ProcessingResponseDto> processingControl;

    private CancellationTokenSource? listenerCancelTokenSource;
    private Task? taskListener;
    private Socket? socketListener;

    private CancellationTokenSource? sessionCancelTokenSource;
    private Task? taskSession;
    private Socket? socketSession;

    private uint sequence = 0;

    public ScpiServer(
        ILogger logger,
        ThunderscopeSettings settings,
        string thunderscopeSerial,
        IPAddress address,
        int port,
        BlockingRequestResponse<ProcessingRequestDto, ProcessingResponseDto> processingControl)
    {
        this.logger = logger;
        this.settings = settings;
        this.thunderscopeSerial = thunderscopeSerial;
        this.address = address;
        this.port = port;
        this.processingControl = processingControl;
    }

    public void Start(SemaphoreSlim startSemaphore)
    {
        listenerCancelTokenSource = new CancellationTokenSource();
        taskListener = Task.Factory.StartNew(() => LoopListener(logger, listenerCancelTokenSource.Token), TaskCreationOptions.LongRunning);
        startSemaphore.Release();
    }

    public void Stop()
    {
        listenerCancelTokenSource?.Cancel();
        sessionCancelTokenSource?.Cancel();
        socketListener?.Close();
        socketSession?.Close();
        taskListener?.Wait();
        taskSession?.Wait();
    }

    private void LoopListener(ILogger logger, CancellationToken cancelToken)
    {
        while (!cancelToken.IsCancellationRequested)
        {
            socketListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socketListener.Bind(new IPEndPoint(address, port));
            socketListener.Listen(backlog: 1);
            logger.LogInformation($"SCPI socket listening {socketListener.LocalEndPoint}");
            try
            {
                while (true)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    var session = socketListener!.Accept();
                    session.NoDelay = true;
                    if (socketSession != null)
                    {
                        logger.LogInformation($"Dropping SCPI session {socketSession?.RemoteEndPoint} and accepting new SCPI session");
                        try { socketSession?.Shutdown(SocketShutdown.Both); } catch { }
                        try { socketSession?.Close(); } catch { }
                        sessionCancelTokenSource?.Cancel();
                    }
                    logger.LogInformation($"SCPI session accepted ({session.RemoteEndPoint})");
                    sessionCancelTokenSource = new CancellationTokenSource();
                    taskSession = Task.Factory.StartNew(() => LoopSession(logger, session, sessionCancelTokenSource.Token), TaskCreationOptions.LongRunning);
                    socketSession = session;
                }
            }
            catch (OperationCanceledException) { }
            catch (SocketException ex)
            {
                logger.LogDebug($"SocketException: {ex.SocketErrorCode}");
            }
            catch (Exception ex)
            {
                logger.LogDebug($"Exception: {ex.Message}");
            }
            finally
            {
                if (!cancelToken.IsCancellationRequested)
                    logger.LogCritical("SCPI socket closed");
                else
                    logger.LogDebug("SCPI socket closed");
                socketListener = null;
            }
        }
    }

    private void LoopSession(ILogger logger, Socket socket, CancellationToken cancelToken)
    {
        string sessionID = socket.RemoteEndPoint?.ToString() ?? "Unknown";
        var buffer = new byte[4096];
        var sb = new StringBuilder();

        try
        {
            while (true)
            {
                cancelToken.ThrowIfCancellationRequested();
                int read = socket.Receive(buffer);
                if (read == 0)
                    break;

                sb.Append(Encoding.UTF8.GetString(buffer, 0, read));
                while (true)
                {
                    int newlineIndex = sb.ToString().IndexOf('\n');
                    if (newlineIndex < 0)
                        break;

                    string message = sb.ToString(0, newlineIndex + 1);
                    sb.Remove(0, newlineIndex + 1);

                    string? response = ProcessSCPICommand(logger, settings, thunderscopeSerial, processingControl, message.TrimEnd('\r', '\n'));

                    if (response != null)
                    {
                        logger.LogDebug("SCPI response: '{String}'", response.Trim());
                        byte[] respBytes = Encoding.UTF8.GetBytes(response);
                        socket.Send(respBytes);
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (SocketException se)
        {
            logger.LogDebug($"SCPI SocketException {se.SocketErrorCode}");
        }
        catch (Exception ex)
        {
            logger.LogDebug($"SCPI Exception: {ex.Message}");
        }
        finally
        {
            logger.LogInformation($"SCPI session dropped ({sessionID})");
            socketSession = null;
            try { socket.Shutdown(SocketShutdown.Both); } catch { }
            try { socket.Close(); } catch { }
        }
    }

    public void OnUpdateSequence(uint seq)
    {
        sequence = seq;
    }

    public string? ProcessSCPICommand(
        ILogger logger,
        ThunderscopeSettings settings,
        string thunderscopeSerial,
        BlockingRequestResponse<ProcessingRequestDto, ProcessingResponseDto> processingControl,
        string message)
    {
        const string InvalidParameters = "One or more invalid parameters";
        const int processingControlTimeoutMs = 1000;
        const int hardwareControlTimeoutMs = 500;

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
                            case "RUN":
                                processingControl.Request.Writer.Write(new ProcessingRun());
                                logger.LogDebug($"{nameof(ProcessingRun)} sent");
                                return null;
                            case "STOP":
                                processingControl.Request.Writer.Write(new ProcessingStop());
                                logger.LogDebug($"{nameof(ProcessingStop)} sent");
                                return null;
                            case "FORCE":
                                processingControl.Request.Writer.Write(new ProcessingForce());
                                logger.LogDebug($"{nameof(ProcessingForce)} sent");
                                return null;
                            case "SINGLE":
                                processingControl.Request.Writer.Write(new ProcessingSetMode(Mode.Single));
                                logger.LogDebug($"{nameof(ProcessingSetMode)} sent");
                                return null;
                            case "NORMAL":
                                processingControl.Request.Writer.Write(new ProcessingSetMode(Mode.Normal));
                                logger.LogDebug($"{nameof(ProcessingSetMode)} sent");
                                return null;
                            case "AUTO":
                                processingControl.Request.Writer.Write(new ProcessingSetMode(Mode.Auto));
                                logger.LogDebug($"{nameof(ProcessingSetMode)} sent");
                                return null;
                            case "STREAM":
                                processingControl.Request.Writer.Write(new ProcessingSetMode(Mode.Stream));
                                logger.LogDebug($"{nameof(ProcessingSetMode)} sent");
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
                                    processingControl.Request.Writer.Write(new HardwareSetRate(rate));
                                    logger.LogDebug($"{nameof(HardwareSetRate)} sent with argument: {rate}");
                                    return null;
                                }
                            case var _ when command.StartsWith("DEPTH") && argument != null:
                                {
                                    var depth = Convert.ToInt32(argument);
                                    processingControl.Request.Writer.Write(new ProcessingSetDepth(depth));
                                    logger.LogDebug($"{nameof(ProcessingSetDepth)} sent with argument: {depth}");
                                    return null;
                                }
                            case var _ when command.StartsWith("RES") && argument != null:
                                {
                                    var resolution = Convert.ToInt32(argument) switch { 8 => AdcResolution.EightBit, 12 => AdcResolution.TwelveBit, _ => AdcResolution.EightBit };
                                    processingControl.Request.Writer.Write(new HardwareSetResolution(resolution));
                                    logger.LogDebug($"{nameof(HardwareSetResolution)} sent with argument: {resolution}");
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
                                    processingControl.Request.Writer.Write(new ProcessingSetTriggerSource(triggerChannel));
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
                                        "TEMP-EVENT" => TriggerType.Event,      // Temporary name, find a better name or SCPI subsystem
                                        _ => null
                                    };

                                    if (triggerType == null)
                                    {
                                        logger.LogWarning("Trigger type parameter not recognised: {Argument}. Valid values: EDGE, WINDOW, RUNT, WIDTH, INTERVAL, BURST, DROPOUT, SLEWRATE", argument);
                                        return null;
                                    }

                                    logger.LogDebug($"Set trigger type to {triggerType}");
                                    processingControl.Request.Writer.Write(new ProcessingSetTriggerType(triggerType.Value));
                                    return null;
                                }
                            case var _ when command.StartsWith("DEL") && argument != null:
                                {
                                    // TRIGger:DELay <arg>
                                    // TRIG:DEL <arg>
                                    long delay = Convert.ToInt64(argument);
                                    logger.LogDebug($"Set trigger delay to {delay}fs");
                                    if (delay < 0)
                                        delay = 0;      // To do: allow negative delays
                                    processingControl.Request.Writer.Write(new ProcessingSetTriggerDelay((ulong)delay));
                                    return null;
                                }
                            case var _ when command.StartsWith("HOLD") && argument != null:
                                {
                                    // TRIGger:HOLDoff <arg>
                                    // TRIG:HOLD <arg>
                                    long holdoff = Convert.ToInt64(argument);
                                    logger.LogDebug($"Set trigger holdoff to {holdoff}fs");
                                    processingControl.Request.Writer.Write(new ProcessingSetTriggerHoldoff((ulong)holdoff));
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
                                    processingControl.Request.Writer.Write(new ProcessingSetTriggerInterpolation(enabled));
                                    return null;
                                }
                            case var _ when command.StartsWith("EDGE:LEV") && argument != null:
                                {
                                    // TRIGger:EDGE:LEVel <arg>
                                    // TRIG:EDGE:LEV <arg>
                                    float level = Convert.ToSingle(argument);
                                    logger.LogDebug($"Set trigger level to {level}V");
                                    processingControl.Request.Writer.Write(new ProcessingSetEdgeTriggerLevel(level));
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
                                    processingControl.Request.Writer.Write(new ProcessingSetEdgeTriggerDirection(type));
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
                                    processingControl.Request.Writer.Write(new HardwareSetChannelEnabled(channelIndex, command == "ON"));
                                    return null;
                                }
                            case var _ when command.StartsWith("BAND") && argument != null:
                                {
                                    // CHANnel1:BANDwidth <arg>
                                    // CHAN1:BAND <arg>
                                    if (GetBandwidth(argument) is not ThunderscopeBandwidth thunderscopeBandwidth)
                                        return null;
                                    processingControl.Request.Writer.Write(new HardwareSetBandwidth(channelIndex, thunderscopeBandwidth));
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
                                    processingControl.Request.Writer.Write(new HardwareSetCoupling(channelIndex, (ThunderscopeCoupling)thunderscopeCoupling));
                                    return null;
                                }
                            case var _ when command.StartsWith("TERM") && argument != null:
                                {
                                    // CHANnel1:TERMination <arg>
                                    // CHAN1:TERM <arg>
                                    if (GetTermination(argument) is not ThunderscopeTermination thunderscopeTermination)
                                        return null;
                                    processingControl.Request.Writer.Write(new HardwareSetTermination(channelIndex, thunderscopeTermination));
                                    return null;
                                }
                            case var _ when command.StartsWith("OFFS") && argument != null:
                                {
                                    // CHANnel1:OFFSet <arg>
                                    // CHAN1:OFFS <arg>
                                    float offset = Convert.ToSingle(argument);
                                    offset = Math.Clamp(offset, -50, 50);     // Change to final values later
                                    processingControl.Request.Writer.Write(new HardwareSetVoltOffset(channelIndex, offset));
                                    return null;
                                }
                            case var _ when command.StartsWith("RANG") && argument != null:
                                {
                                    float range = Convert.ToSingle(argument);
                                    range = Math.Clamp(range, -50, 50);       // Change to final values later
                                    processingControl.Request.Writer.Write(new HardwareSetVoltFullScale(channelIndex, range));
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

                                        processingControl.Request.Writer.Write(new HardwareSetChannelManualControl(channelIndex, channel));
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
                                        adcCal.FineGainBranch1 = (byte)(Convert.ToSByte(args[0]) & 0x7F);
                                        adcCal.FineGainBranch2 = (byte)(Convert.ToSByte(args[1]) & 0x7F);
                                        adcCal.FineGainBranch3 = (byte)(Convert.ToSByte(args[2]) & 0x7F);
                                        adcCal.FineGainBranch4 = (byte)(Convert.ToSByte(args[3]) & 0x7F);
                                        adcCal.FineGainBranch5 = (byte)(Convert.ToSByte(args[4]) & 0x7F);
                                        adcCal.FineGainBranch6 = (byte)(Convert.ToSByte(args[5]) & 0x7F);
                                        adcCal.FineGainBranch7 = (byte)(Convert.ToSByte(args[6]) & 0x7F);
                                        adcCal.FineGainBranch8 = (byte)(Convert.ToSByte(args[7]) & 0x7F);
                                        processingControl.Request.Writer.Write(new HardwareSetAdcCalibration(adcCal));
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
                        var engineVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "NO_VERSION";
                        return $"EEVengers,ThunderScope,{thunderscopeSerial},{engineVersion}\n";
                    case "STATE?":
                        {
                            while (processingControl.Response.Reader.TryRead(out var _, 10)) { }
                            processingControl.Request.Writer.Write(new ProcessingGetStateRequest());
                            if (processingControl.Response.Reader.TryRead(out var response, processingControlTimeoutMs))
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
                            while (processingControl.Response.Reader.TryRead(out var _, 10)) { }
                            processingControl.Request.Writer.Write(new ProcessingGetModeRequest());
                            if (processingControl.Response.Reader.TryRead(out var response, processingControlTimeoutMs))
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
                    case "SEQNUM?":
                        {
                            return $"{sequence}\n";
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
                            while (processingControl.Response.Reader.TryRead(out var _, 10)) { }
                            processingControl.Request.Writer.Write(new HardwareGetResolutionRequest());
                            if (processingControl.Response.Reader.TryRead(out var response, processingControlTimeoutMs))
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
                                            logger.LogError($"{subject}:RES? - Unhandled response from {nameof(processingControl.Response.Reader)}");
                                            break;
                                    }
                                }
                                else
                                {
                                    logger.LogError($"{subject}:RES? - Invalid response from {nameof(processingControl.Response.Reader)}");
                                }
                            }
                            else
                            {
                                logger.LogError($"{subject}:RES? - No response from {nameof(processingControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }

                }
            }
            else if (subject?.StartsWith("TRIG") == true)
            {
                // Trigger query commands
                while (processingControl.Response.Reader.TryRead(out var _, 10)) { }
                switch (command)
                {
                    case var _ when command.StartsWith("SOU"):
                        {
                            processingControl.Request.Writer.Write(new ProcessingGetTriggerSourceRequest());
                            if (processingControl.Response.Reader.TryRead(out var response, processingControlTimeoutMs))
                            {
                                switch (response)
                                {
                                    case ProcessingGetTriggerSourceResponse triggerSourceResponse:
                                        if(triggerSourceResponse.Channel == TriggerChannel.None)
                                            return "NONE\n";
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
                            if (processingControl.Response.Reader.TryRead(out var response, processingControlTimeoutMs))
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
                            if (processingControl.Response.Reader.TryRead(out var response, processingControlTimeoutMs))
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
                            if (processingControl.Response.Reader.TryRead(out var response, processingControlTimeoutMs))
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
                            if (processingControl.Response.Reader.TryRead(out var response, processingControlTimeoutMs))
                            {
                                switch (response)
                                {
                                    case ProcessingGetTriggerInterpolationResponse triggerInterpolationResponse:
                                        return $"{(triggerInterpolationResponse.Enabled ? "true" : "false")}\n";
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
                            if (processingControl.Response.Reader.TryRead(out var response, processingControlTimeoutMs))
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
                            if (processingControl.Response.Reader.TryRead(out var response, processingControlTimeoutMs))
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

                while (processingControl.Response.Reader.TryRead(out var _, 10)) { }
                switch (command)
                {
                    case var _ when command.Equals("STATE?", StringComparison.OrdinalIgnoreCase):
                        {
                            processingControl.Request.Writer.Write(new HardwareGetEnabledRequest());
                            if (processingControl.Response.Reader.TryRead(out var response, hardwareControlTimeoutMs))
                            {
                                if (response is HardwareGetEnabledResponse processingGetEnabledResponse)
                                {
                                    return ((processingGetEnabledResponse.EnabledChannels >> channelIndex) & 0x1) > 0 ? "ON\n" : "OFF\n";
                                }
                                else
                                {
                                    logger.LogError($"{subject}:STATE? - Invalid response from {nameof(processingControl.Response.Reader)}");
                                }
                            }
                            else
                            {
                                logger.LogError($"{subject}:STATE? - No response from {nameof(processingControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }
                    case var _ when command.StartsWith("BAND", StringComparison.OrdinalIgnoreCase):
                        {
                            processingControl.Request.Writer.Write(new HardwareGetBandwidthRequest(channelIndex));
                            if (processingControl.Response.Reader.TryRead(out var response, hardwareControlTimeoutMs))
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
                                        _ => throw new NotImplementedException()
                                    };
                                    return bandwidth + "\n";
                                }
                                else
                                {
                                    logger.LogError($"{subject}:BAND? - Invalid response from {nameof(processingControl.Response.Reader)}");
                                }
                            }
                            else
                            {
                                logger.LogError($"{subject}:BAND? - No response from {nameof(processingControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }
                    case var _ when command.StartsWith("COUP", StringComparison.OrdinalIgnoreCase):
                        {
                            processingControl.Request.Writer.Write(new HardwareGetCouplingRequest(channelIndex));
                            if (processingControl.Response.Reader.TryRead(out var response, hardwareControlTimeoutMs))
                            {
                                if (response is HardwareGetCouplingResponse hardwareGetCouplingResponse)
                                {
                                    string coupling = hardwareGetCouplingResponse.Coupling switch
                                    {
                                        ThunderscopeCoupling.DC => "DC",
                                        ThunderscopeCoupling.AC => "AC",
                                        _ => throw new NotImplementedException()
                                    };
                                    return coupling + "\n";
                                }
                                else
                                {
                                    logger.LogError($"{subject}:COUP? - Invalid response from {nameof(processingControl.Response.Reader)}");
                                }
                            }
                            else
                            {
                                logger.LogError($"{subject}:COUP? - No response from {nameof(processingControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }
                    case var _ when command.StartsWith("TERM", StringComparison.OrdinalIgnoreCase):
                        {
                            processingControl.Request.Writer.Write(new HardwareGetTerminationRequest(channelIndex));
                            if (processingControl.Response.Reader.TryRead(out var response, hardwareControlTimeoutMs))
                            {
                                if (response is HardwareGetTerminationResponse hardwareGetTerminationResponse)
                                {
                                    // RANG/OFFS returns requested values (requested can be changed by driver), TERM returns actual
                                    string termination = hardwareGetTerminationResponse.ActualTermination switch
                                    {
                                        ThunderscopeTermination.OneMegaohm => "1M",
                                        ThunderscopeTermination.FiftyOhm => "50",
                                        _ => throw new NotImplementedException()
                                    };
                                    return termination + "\n";
                                }
                                else
                                {
                                    logger.LogError($"{subject}:TERM? - Invalid response from {nameof(processingControl.Response.Reader)}");
                                }
                            }
                            else
                            {
                                logger.LogError($"{subject}:TERM? - No response from {nameof(processingControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }
                    case var _ when command.StartsWith("OFFS", StringComparison.OrdinalIgnoreCase):
                        {
                            processingControl.Request.Writer.Write(new HardwareGetVoltOffsetRequest(channelIndex));
                            if (processingControl.Response.Reader.TryRead(out var response, hardwareControlTimeoutMs))
                            {
                                if (response is HardwareGetVoltOffsetResponse hardwareGetVoltOffsetResponse)
                                    return $"{hardwareGetVoltOffsetResponse.RequestedVoltOffset:0.######}\n";
                                logger.LogError($"{subject}:OFFS? - Invalid response from {nameof(processingControl.Response.Reader)}");
                            }
                            else
                            {
                                logger.LogError($"{subject}:OFFS? - No response from {nameof(processingControl.Response.Reader)}");
                            }
                            return "Error: No/bad response from channel.\n";
                        }
                    case var _ when command.StartsWith("RANG", StringComparison.OrdinalIgnoreCase):
                        {
                            processingControl.Request.Writer.Write(new HardwareGetVoltFullScaleRequest(channelIndex));
                            if (processingControl.Response.Reader.TryRead(out var response, hardwareControlTimeoutMs))
                            {
                                if (response is HardwareGetVoltFullScaleResponse hardwareGetVoltFullScaleResponse)
                                {
                                    return $"{hardwareGetVoltFullScaleResponse.RequestedVoltFullScale:0.######}\n";
                                }
                                else
                                {
                                    logger.LogError($"{subject}:RANG? - Invalid response from {nameof(processingControl.Response.Reader)}");
                                }
                            }
                            else
                            {
                                logger.LogError($"{subject}:RANG? - No response from {nameof(processingControl.Response.Reader)}");
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
            while (processingControl.Response.Reader.TryRead(out var _, 10)) { }
            processingControl.Request.Writer.Write(new ProcessingGetRatesRequest());
            if (processingControl.Response.Reader.TryRead(out var response, processingControlTimeoutMs))
            {
                switch (response)
                {
                    case ProcessingGetRatesResponse processingGetRatesResponse:
                        return $"{string.Join(",", processingGetRatesResponse.SampleRatesHz)}\n";
                    default:
                        logger.LogError($"RATES? - Invalid response from {nameof(processingControl.Response.Reader)}");
                        break;
                }
            }
            logger.LogError($"RATES? - No response from {nameof(processingControl.Response.Reader)}");
            return "Error: No/bad response from channel.\n";
        }

        string GetRate()
        {
            while (processingControl.Response.Reader.TryRead(out var _, 10)) { }
            processingControl.Request.Writer.Write(new HardwareGetRateRequest());
            if (processingControl.Response.Reader.TryRead(out var response, processingControlTimeoutMs))
            {
                switch (response)
                {
                    case HardwareGetRateResponse hardwareGetRateResponse:
                        return $"{hardwareGetRateResponse.SampleRateHz}\n";
                    default:
                        logger.LogError($"RATE? - Invalid response from {nameof(processingControl.Response.Reader)}");
                        break;
                }
            }
            logger.LogError($"RATE? - No response from {nameof(processingControl.Response.Reader)}");
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
            return $"{string.Join(",", depths)}\n";
        }

        string GetDepth()
        {
            processingControl.Request.Writer.Write(new ProcessingGetDepthRequest());
            if (processingControl.Response.Reader.TryRead(out var response, processingControlTimeoutMs))
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
