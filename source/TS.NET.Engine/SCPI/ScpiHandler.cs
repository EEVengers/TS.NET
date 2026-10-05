using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using static System.FormattableString;

namespace TS.NET.Engine;

internal class ScpiHandler
{
    private const int ProcessingControlTimeoutMs = 500;
    private const int HardwareControlTimeoutMs = 500;
    private const string ResponseError = "Error: No/bad response from channel.\n";

    private readonly ILogger logger;
    private readonly ThunderscopeSettings settings;
    private readonly string thunderscopeSerial;
    private readonly BlockingRequestResponse<ProcessingRequest, ProcessingResponse> processingControl;
    private readonly CommandNode commands = new();
    private readonly Lock commandLock = new();

    private uint requestIdCounter;
    private uint sequence;

    public ScpiHandler(ILogger logger, ThunderscopeSettings settings, string thunderscopeSerial,
        BlockingRequestResponse<ProcessingRequest, ProcessingResponse> processingControl)
    {
        this.logger = logger;
        this.settings = settings;
        this.thunderscopeSerial = thunderscopeSerial;
        this.processingControl = processingControl;

        Register("RUN", Run);
        Register("STOP", Stop);
        Register("FORCE", Force);
        Register("SINGLE", Single);
        Register("NORMAL", Normal);
        Register("AUTO", Auto);
        Register("STREAM", Stream);
        Register("*IDN?", GetIdentification);
        Register("*OPC?", GetOperationComplete);
        Register("STATE?", GetRunState);
        Register("MODE?", GetMode);
        Register("SEQNUM?", GetSequenceNumber);
        Register("TEMPerature?", GetTemperature);

        Register("ACQuisition:RATE", SetAcquisitionRate);
        Register("ACQuisition:RATE?", GetAcquisitionRate);
        Register("ACQuisition:DEPTH", SetAcquisitionDepth);
        Register("ACQuisition:DEPTH?", GetAcquisitionDepth);
        Register("ACQuisition:RESolution", SetAcquisitionResolution);
        Register("ACQuisition:RESolution?", GetAcquisitionResolution);
        Register("ACQuisition:RATES?", GetAcquisitionRates);
        Register("ACQuisition:DEPTHS?", GetAcquisitionDepths);

        Register("TRIGger:SOUrce", SetTriggerSource);
        Register("TRIGger:SOUrce?", GetTriggerSource);
        Register("TRIGger:TYPE", SetTriggerType);
        Register("TRIGger:TYPE?", GetTriggerType);
        Register("TRIGger:DELay", SetTriggerDelay);
        Register("TRIGger:DELay?", GetTriggerDelay);
        Register("TRIGger:HOLDoff", SetTriggerHoldoff);
        Register("TRIGger:HOLDoff?", GetTriggerHoldoff);
        Register("TRIGger:INTERpolation", SetTriggerInterpolation);
        Register("TRIGger:INTERpolation?", GetTriggerInterpolation);
        Register("TRIGger:EDGE:LEVel", SetTriggerEdgeLevel);
        Register("TRIGger:EDGE:LEVel?", GetTriggerEdgeLevel);
        Register("TRIGger:EDGE:DIRection", SetTriggerEdgeDirection);
        Register("TRIGger:EDGE:DIRection?", GetTriggerEdgeDirection);
        Register("TRIGger:EDGE:HYSteresis", SetTriggerEdgeHysteresis);
        Register("TRIGger:EDGE:HYSteresis?", GetTriggerEdgeHysteresis);
        // Register("TRIGger:WINDow:HYSteresis", SetTriggerWindowHysteresis);
        // Register("TRIGger:WINDow:HYSteresis?", GetTriggerWindowHysteresis);
        // Register("TRIGger:WINDow:UPPer", SetTriggerWindowUpper);
        // Register("TRIGger:WINDow:UPPer?", GetTriggerWindowUpper);
        // Register("TRIGger:WINDow:LOWer", SetTriggerWindowLower);
        // Register("TRIGger:WINDow:LOWer?", GetTriggerWindowLower);
        // Register("TRIGger:WINDow:DIRection", SetTriggerWindowDirection);
        // Register("TRIGger:WINDow:DIRection?", GetTriggerWindowDirection);
        Register("TRIGger:BURST:LEVel", SetTriggerBurstLevel);
        Register("TRIGger:BURST:LEVel?", GetTriggerBurstLevel);
        Register("TRIGger:BURST:DIRection", SetTriggerBurstDirection);
        Register("TRIGger:BURST:DIRection?", GetTriggerBurstDirection);
        Register("TRIGger:BURST:EDGE?", GetTriggerBurstDirection);
        Register("TRIGger:BURST:HYSteresis", SetTriggerBurstHysteresis);
        Register("TRIGger:BURST:HYSteresis?", GetTriggerBurstHysteresis);
        Register("TRIGger:BURST:QUIET:UPPER", SetTriggerBurstQuietUpper);
        Register("TRIGger:BURST:QUIET:UPPER?", GetTriggerBurstQuietUpper);
        Register("TRIGger:BURST:QUIET:LOWER", SetTriggerBurstQuietLower);
        Register("TRIGger:BURST:QUIET:LOWER?", GetTriggerBurstQuietLower);
        Register("TRIGger:BURST:QUIET:TIME", SetTriggerBurstQuietTime);
        Register("TRIGger:BURST:QUIET:TIME?", GetTriggerBurstQuietTime);

        for (int channelIndex = 0; channelIndex < 4; channelIndex++)
        {
            string prefix = $"CHANnel{channelIndex + 1}:";
            Register(prefix + "ON", EnableChannel, channelIndex);
            Register(prefix + "OFF", DisableChannel, channelIndex);
            Register(prefix + "STATE?", GetChannelState, channelIndex);
            Register(prefix + "BANDwidth", SetChannelBandwidth, channelIndex);
            Register(prefix + "BANDwidth?", GetChannelBandwidth, channelIndex);
            Register(prefix + "COUPling", SetChannelCoupling, channelIndex);
            Register(prefix + "COUPling?", GetChannelCoupling, channelIndex);
            Register(prefix + "TERMination", SetChannelTermination, channelIndex);
            Register(prefix + "TERMination?", GetChannelTermination, channelIndex);
            Register(prefix + "TERMination:ACTual?", GetChannelActualTermination, channelIndex);
            Register(prefix + "OFFSet", SetChannelOffset, channelIndex);
            Register(prefix + "OFFSet?", GetChannelOffset, channelIndex);
            Register(prefix + "OFFSet:ACTual?", GetChannelActualOffset, channelIndex);
            Register(prefix + "RANGe", SetChannelRange, channelIndex);
            Register(prefix + "RANGe?", GetChannelRange, channelIndex);
            Register(prefix + "RANGe:ACTual?", GetChannelActualRange, channelIndex);
        }

        Register("REFCLock:MODE", SetReferenceClockMode);
        Register("REFCLock:FREQuency", SetReferenceClockFrequency);

        Register("DEBUG:FRONTEND", SetFrontendManualControl);
        Register("DEBUG:BRANCHGAINS", SetBranchGainsManualControl);
    }

    public void OnUpdateSequence(uint seq)
    {
        Volatile.Write(ref sequence, seq);
    }

    public string? ProcessSCPICommand(string message, CancellationToken cancellationToken = default)
    {
        lock (commandLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogDebug($"SCPI request: {message}");
            try
            {
                var command = ParsedCommand.Parse(message);
                var handler = commands.Resolve(command.Path, command.IsQuery);
                if (handler == null)
                {
                    logger.LogWarning($"Unknown SCPI command: {message}");
                    return null;
                }

                var context = new CommandContext(this, handler.Path, command.Argument, handler.ChannelIndex, cancellationToken);
                return handler.Execute(context);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                logger.LogWarning($"Invalid SCPI parameter in command '{message}': {ex.Message}");
                return null;
            }
        }
    }

    private string? Run(CommandContext context)
    {
        return context.Action(new ProcessingRun());
    }

    private string? Stop(CommandContext context)
    {
        return context.Action(new ProcessingStop());
    }

    private string? Force(CommandContext context)
    {
        return context.Action(new ProcessingForce());
    }

    private string? Single(CommandContext context)
    {
        return context.Action(new ProcessingSetMode(Mode.Single));
    }

    private string? Normal(CommandContext context)
    {
        return context.Action(new ProcessingSetMode(Mode.Normal));
    }

    private string? Auto(CommandContext context)
    {
        return context.Action(new ProcessingSetMode(Mode.Auto));
    }

    private string? Stream(CommandContext context)
    {
        return context.Action(new ProcessingSetMode(Mode.Stream));
    }

    private string GetIdentification(CommandContext context)
    {
        var version = typeof(ScpiHandler).Assembly.GetName().Version?.ToString(3) ?? "NO_VERSION";
        return context.Reply($"EEVengers,ThunderScope,{thunderscopeSerial},{version}\n");
    }

    private string GetOperationComplete(CommandContext context)
    {
        return context.Get<ProcessingGetOperationCompleteResponse>(new ProcessingGetOperationCompleteRequest(), response => "1\n");
    }

    private string GetRunState(CommandContext context)
    {
        return context.Get<ProcessingGetStateResponse>(new ProcessingGetStateRequest(), r => r.Run ? "RUN\n" : "STOP\n");
    }

    private string GetMode(CommandContext context)
    {
        return context.Get<ProcessingGetModeResponse>(new ProcessingGetModeRequest(), r => $"{r.Mode.ToString().ToUpperInvariant()}\n");
    }

    private string GetSequenceNumber(CommandContext context)
    {
        return context.Reply(Invariant($"{Volatile.Read(ref sequence)}\n"));
    }

    private string GetTemperature(CommandContext context)
    {
        return context.Get<HardwareGetTemperatureResponse>(new HardwareGetTemperatureRequest(), r => Invariant($"{r.Temperature:F1}\n"));
    }

    private string? SetAcquisitionRate(CommandContext context)
    {
        return context.Set(new HardwareSetRate(ParseUInt64(context.Argument)));
    }

    private string GetAcquisitionRate(CommandContext context)
    {
        return context.Get<HardwareGetRateResponse>(new HardwareGetRateRequest(), r => Invariant($"{r.SampleRateHz}\n"));
    }

    private string? SetAcquisitionDepth(CommandContext context)
    {
        return context.Set(new ProcessingSetDepth(checked((int)ParseUInt32(context.Argument))));
    }

    private string GetAcquisitionDepth(CommandContext context)
    {
        return context.Get<ProcessingGetDepthResponse>(new ProcessingGetDepthRequest(), r => Invariant($"{r.Depth}\n"));
    }

    private string? SetAcquisitionResolution(CommandContext context)
    {
        uint bits = ParseUInt32(context.Argument);
        if (bits != 8 && bits != 12)
            logger.LogWarning($"Unsupported ADC resolution {bits}; defaulting to 8-bit");
        return context.Set(new HardwareSetResolution(bits == 12 ? AdcResolution.TwelveBit : AdcResolution.EightBit));
    }

    private string GetAcquisitionResolution(CommandContext context)
    {
        return context.Get<HardwareGetResolutionResponse>(new HardwareGetResolutionRequest(), r => r.Resolution switch
        {
            AdcResolution.EightBit => "8\n",
            AdcResolution.TwelveBit => "12\n",
            _ => throw new InvalidOperationException($"Unexpected ADC resolution: {r.Resolution}")
        });
    }

    private string GetAcquisitionRates(CommandContext context)
    {
        return context.Get<ProcessingGetRatesResponse>(new ProcessingGetRatesRequest(),
            r => string.Join(",", r.SampleRatesHz.Select(rate => rate.ToString(CultureInfo.InvariantCulture))) + "\n");
    }

    private string GetAcquisitionDepths(CommandContext context)
    {
        return context.Reply(GetDepths());
    }

    private string? SetTriggerSource(CommandContext context)
    {
        return context.Set(new ProcessingSetTriggerSource(ParseTriggerSource(context.Argument)));
    }

    private string GetTriggerSource(CommandContext context)
    {
        return context.Get<ProcessingGetTriggerSourceResponse>(new ProcessingGetTriggerSourceRequest(), r => r.Channel switch
        {
            TriggerChannel.None => "NONE\n",
            TriggerChannel.External => "EXT\n",
            _ => Invariant($"CHAN{(int)r.Channel}\n")
        });
    }

    private string? SetTriggerType(CommandContext context)
    {
        return context.Set(new ProcessingSetTriggerType(ParseChoice(context.Argument,
            ("EDGE", TriggerType.Edge), ("BURST", TriggerType.Burst))));
    }

    private string GetTriggerType(CommandContext context)
    {
        return context.Get<ProcessingGetTriggerTypeResponse>(new ProcessingGetTriggerTypeRequest(), r => $"{r.Type.ToString().ToUpperInvariant()}\n");
    }

    private string? SetTriggerDelay(CommandContext context)
    {
        return context.Set(new ProcessingSetTriggerDelay((ulong)Math.Max(0, ParseInt64(context.Argument))));
    }

    private string GetTriggerDelay(CommandContext context)
    {
        return context.Get<ProcessingGetTriggerDelayResponse>(new ProcessingGetTriggerDelayRequest(), r => Invariant($"{r.Femtoseconds}\n"));
    }

    private string? SetTriggerHoldoff(CommandContext context)
    {
        return context.Set(new ProcessingSetTriggerHoldoff(ParseUInt64(context.Argument)));
    }

    private string GetTriggerHoldoff(CommandContext context)
    {
        return context.Get<ProcessingGetTriggerHoldoffResponse>(new ProcessingGetTriggerHoldoffRequest(), r => Invariant($"{r.Femtoseconds}\n"));
    }

    private string? SetTriggerInterpolation(CommandContext context)
    {
        string argument = context.Argument;
        bool enabled;
        switch (argument.ToUpperInvariant())
        {
            case "TRUE" or "1": enabled = true; break;
            case "FALSE" or "0": enabled = false; break;
            default:
                logger.LogWarning($"Unsupported interpolation value {argument}; defaulting to true");
                enabled = true;
                break;
        }
        return context.Set(new ProcessingSetTriggerInterpolation(enabled));
    }

    private string GetTriggerInterpolation(CommandContext context)
    {
        return context.Get<ProcessingGetTriggerInterpolationResponse>(new ProcessingGetTriggerInterpolationRequest(), r => r.Enabled ? "true\n" : "false\n");
    }

    private string? SetTriggerEdgeLevel(CommandContext context)
    {
        return context.Set(new ProcessingSetEdgeTriggerLevel(ParseFloat(context.Argument)));
    }

    private string GetTriggerEdgeLevel(CommandContext context)
    {
        return context.Get<ProcessingGetEdgeTriggerLevelResponse>(new ProcessingGetEdgeTriggerLevelRequest(), r => Invariant($"{r.LevelVolts:0.######}\n"));
    }

    private string? SetTriggerEdgeDirection(CommandContext context)
    {
        return context.Set(new ProcessingSetEdgeTriggerDirection(ParseChoice(context.Argument,
            ("RISING", EdgeDirection.Rising), ("FALLING", EdgeDirection.Falling), ("ANY", EdgeDirection.Any))));
    }

    private string GetTriggerEdgeDirection(CommandContext context)
    {
        return context.Get<ProcessingGetEdgeTriggerDirectionResponse>(new ProcessingGetEdgeTriggerDirectionRequest(), r => $"{r.Direction.ToString().ToUpperInvariant()}\n");
    }

    private string? SetTriggerEdgeHysteresis(CommandContext context)
    {
        return context.Set(new ProcessingSetEdgeTriggerHysteresis(ParseFloat(context.Argument)));
    }

    private string GetTriggerEdgeHysteresis(CommandContext context)
    {
        return context.Get<ProcessingGetEdgeTriggerHysteresisResponse>(new ProcessingGetEdgeTriggerHysteresisRequest(), r => Invariant($"{r.Percent:0.######}\n"));
    }

    private string? SetTriggerWindowHysteresis(CommandContext context)
    {
        return context.Set(new ProcessingSetWindowTriggerHysteresis(ParseFloat(context.Argument)));
    }

    private string GetTriggerWindowHysteresis(CommandContext context)
    {
        return context.Get<ProcessingGetWindowTriggerHysteresisResponse>(new ProcessingGetWindowTriggerHysteresisRequest(),
            r => Invariant($"{r.Percent:0.######}\n"));
    }

    private string? SetTriggerWindowUpper(CommandContext context)
    {
        return context.Set(new ProcessingSetWindowTriggerUpperLevel(ParseFloat(context.Argument)));
    }

    private string GetTriggerWindowUpper(CommandContext context)
    {
        return context.Get<ProcessingGetWindowTriggerUpperLevelResponse>(new ProcessingGetWindowTriggerUpperLevelRequest(),
            r => Invariant($"{r.LevelVolts:0.######}\n"));
    }

    private string? SetTriggerWindowLower(CommandContext context)
    {
        return context.Set(new ProcessingSetWindowTriggerLowerLevel(ParseFloat(context.Argument)));
    }

    private string GetTriggerWindowLower(CommandContext context)
    {
        return context.Get<ProcessingGetWindowTriggerLowerLevelResponse>(new ProcessingGetWindowTriggerLowerLevelRequest(),
            r => Invariant($"{r.LevelVolts:0.######}\n"));
    }

    private string? SetTriggerWindowDirection(CommandContext context)
    {
        return context.Set(new ProcessingSetWindowTriggerDirection(ParseChoice(context.Argument,
            ("ENTER", WindowDirection.Enter), ("EXIT", WindowDirection.Exit))));
    }

    private string GetTriggerWindowDirection(CommandContext context)
    {
        return context.Get<ProcessingGetWindowTriggerDirectionResponse>(new ProcessingGetWindowTriggerDirectionRequest(),
            r => $"{r.Direction.ToString().ToUpperInvariant()}\n");
    }

    private string? SetTriggerBurstLevel(CommandContext context)
    {
        return context.Set(new ProcessingSetBurstTriggerLevel(ParseFloat(context.Argument)));
    }

    private string GetTriggerBurstLevel(CommandContext context)
    {
        return context.Get<ProcessingGetBurstTriggerLevelResponse>(new ProcessingGetBurstTriggerLevelRequest(), r => Invariant($"{r.LevelVolts:0.######}\n"));
    }

    private string? SetTriggerBurstDirection(CommandContext context)
    {
        return context.Set(new ProcessingSetBurstTriggerDirection(ParseChoice(context.Argument,
            ("RISING", BurstEdgeDirection.Rising), ("FALLING", BurstEdgeDirection.Falling))));
    }

    private string GetTriggerBurstDirection(CommandContext context)
    {
        return context.Get<ProcessingGetBurstTriggerDirectionResponse>(new ProcessingGetBurstTriggerDirectionRequest(), r => $"{r.Direction.ToString().ToUpperInvariant()}\n");
    }

    private string? SetTriggerBurstHysteresis(CommandContext context)
    {
        return context.Set(new ProcessingSetBurstTriggerHysteresis(ParseFloat(context.Argument)));
    }

    private string GetTriggerBurstHysteresis(CommandContext context)
    {
        return context.Get<ProcessingGetBurstTriggerHysteresisResponse>(new ProcessingGetBurstTriggerHysteresisRequest(), r => Invariant($"{r.Percent:0.######}\n"));
    }

    private string? SetTriggerBurstQuietUpper(CommandContext context)
    {
        return context.Set(new ProcessingSetBurstTriggerQuietUpperLevel(ParseFloat(context.Argument)));
    }

    private string GetTriggerBurstQuietUpper(CommandContext context)
    {
        return context.Get<ProcessingGetBurstTriggerQuietUpperLevelResponse>(new ProcessingGetBurstTriggerQuietUpperLevelRequest(), r => Invariant($"{r.LevelVolts:0.######}\n"));
    }

    private string? SetTriggerBurstQuietLower(CommandContext context)
    {
        return context.Set(new ProcessingSetBurstTriggerQuietLowerLevel(ParseFloat(context.Argument)));
    }

    private string GetTriggerBurstQuietLower(CommandContext context)
    {
        return context.Get<ProcessingGetBurstTriggerQuietLowerLevelResponse>(new ProcessingGetBurstTriggerQuietLowerLevelRequest(), r => Invariant($"{r.LevelVolts:0.######}\n"));
    }

    private string? SetTriggerBurstQuietTime(CommandContext context)
    {
        return context.Set(new ProcessingSetBurstTriggerQuietTime(ParseInt64(context.Argument)));
    }

    private string GetTriggerBurstQuietTime(CommandContext context)
    {
        return context.Get<ProcessingGetBurstTriggerQuietTimeResponse>(new ProcessingGetBurstTriggerQuietTimeRequest(), r => Invariant($"{r.Femtoseconds}\n"));
    }

    private string? EnableChannel(CommandContext context)
    {
        return context.Action(new HardwareSetChannelEnabled(context.ChannelIndex, true));
    }

    private string? DisableChannel(CommandContext context)
    {
        return context.Action(new HardwareSetChannelEnabled(context.ChannelIndex, false));
    }

    private string GetChannelState(CommandContext context)
    {
        return context.Get<HardwareGetEnabledResponse>(new HardwareGetEnabledRequest(),
            r => ((r.EnabledChannels >> context.ChannelIndex) & 1) != 0 ? "ON\n" : "OFF\n", HardwareControlTimeoutMs);
    }

    private string? SetChannelBandwidth(CommandContext context)
    {
        return context.Set(new HardwareSetBandwidth(context.ChannelIndex, ParseBandwidth(context.Argument)));
    }

    private string GetChannelBandwidth(CommandContext context)
    {
        return context.Get<HardwareGetBandwidthResponse>(new HardwareGetBandwidthRequest(context.ChannelIndex),
            r => FormatBandwidth(r.Bandwidth) + "\n", HardwareControlTimeoutMs);
    }

    private string? SetChannelCoupling(CommandContext context)
    {
        return context.Set(new HardwareSetCoupling(context.ChannelIndex, ParseCoupling(context.Argument)));
    }

    private string GetChannelCoupling(CommandContext context)
    {
        return context.Get<HardwareGetCouplingResponse>(new HardwareGetCouplingRequest(context.ChannelIndex), r => r.Coupling switch
        {
            ThunderscopeCoupling.DC => "DC\n",
            ThunderscopeCoupling.AC => "AC\n",
            _ => throw new InvalidOperationException($"Unexpected coupling: {r.Coupling}")
        }, HardwareControlTimeoutMs);
    }

    private string? SetChannelTermination(CommandContext context)
    {
        return context.Set(new HardwareSetTermination(context.ChannelIndex, ParseTermination(context.Argument)));
    }

    private string GetChannelTermination(CommandContext context)
    {
        return context.Get<HardwareGetTerminationResponse>(new HardwareGetTerminationRequest(context.ChannelIndex),
            r => FormatTermination(r.RequestedTermination) + "\n", HardwareControlTimeoutMs);
    }

    private string GetChannelActualTermination(CommandContext context)
    {
        return context.Get<HardwareGetTerminationResponse>(new HardwareGetTerminationRequest(context.ChannelIndex),
            r => FormatTermination(r.ActualTermination) + "\n", HardwareControlTimeoutMs);
    }

    private string? SetChannelOffset(CommandContext context)
    {
        return context.Set(new HardwareSetVoltOffset(context.ChannelIndex, Math.Clamp(ParseFloat(context.Argument), -50, 50)));
    }

    private string GetChannelOffset(CommandContext context)
    {
        return context.Get<HardwareGetVoltOffsetResponse>(new HardwareGetVoltOffsetRequest(context.ChannelIndex),
            r => Invariant($"{r.RequestedVoltOffset:0.######}\n"), HardwareControlTimeoutMs);
    }

    private string GetChannelActualOffset(CommandContext context)
    {
        return context.Get<HardwareGetVoltOffsetResponse>(new HardwareGetVoltOffsetRequest(context.ChannelIndex), r => Invariant($"{r.ActualVoltOffset:0.######}\n"), HardwareControlTimeoutMs);
    }

    private string? SetChannelRange(CommandContext context)
    {
        return context.Set(new HardwareSetVoltFullScale(context.ChannelIndex, Math.Clamp(ParseFloat(context.Argument), -50, 50)));
    }

    private string GetChannelRange(CommandContext context)
    {
        return context.Get<HardwareGetVoltFullScaleResponse>(new HardwareGetVoltFullScaleRequest(context.ChannelIndex), r => Invariant($"{r.RequestedVoltFullScale:0.######}\n"), HardwareControlTimeoutMs);
    }

    private string GetChannelActualRange(CommandContext context)
    {
        return context.Get<HardwareGetVoltFullScaleResponse>(new HardwareGetVoltFullScaleRequest(context.ChannelIndex), r => Invariant($"{r.ActualVoltFullScale:0.######}\n"), HardwareControlTimeoutMs);
    }

    private string? SetReferenceClockMode(CommandContext context)
    {
        return context.Set(new HardwareSetRefClockMode(ParseChoice(context.Argument, ("IN", ThunderscopeRefClockMode.Input), ("OUT", ThunderscopeRefClockMode.Output), ("OFF", ThunderscopeRefClockMode.Disabled))));
    }

    private string? SetReferenceClockFrequency(CommandContext context)
    {
        return context.Set(new HardwareSetRefClockFrequency(ParseUInt32(context.Argument)));
    }

    private string? SetFrontendManualControl(CommandContext context)
    {
        var args = SplitArguments(context.Argument, 9);
        int channelIndex = ParseChannelIndex(args[0]);
        var channel = new ThunderscopeChannelFrontendManualControl
        {
            Coupling = ParseCoupling(args[1]),
            Termination = ParseTermination(args[2]),
            Attenuator = byte.Parse(args[3], CultureInfo.InvariantCulture),
            DAC = ushort.Parse(args[4], CultureInfo.InvariantCulture),
            DPOT = byte.Parse(args[5], CultureInfo.InvariantCulture),
            PgaLadderAttenuation = byte.Parse(args[6], CultureInfo.InvariantCulture),
            PgaHighGain = byte.Parse(args[7], CultureInfo.InvariantCulture),
            PgaFilter = ParseBandwidth(args[8])
        };
        return context.Set(new HardwareSetChannelManualControl(channelIndex, channel));
    }

    private string? SetBranchGainsManualControl(CommandContext context)
    {
        var args = SplitArguments(context.Argument, 8);
        var gains = args.Select(value => (byte)(sbyte.Parse(value, CultureInfo.InvariantCulture) & 0x7F)).ToArray();
        return context.Set(new HardwareSetAdcBranchGainsManualControl(gains));
    }

    private void Register(string path, Func<CommandContext, string?> execute, int? channelIndex = null)
    {
        bool isQuery = path.EndsWith('?');
        string header = isQuery ? path[..^1] : path;
        commands.Register(header, isQuery, new CommandHandler(header, channelIndex, execute));
    }

    private string? Send(ProcessingRequestDto request, CancellationToken cancellationToken)
    {
        SendRequest(request, cancellationToken);
        logger.LogDebug($"SCPI sent {request}");
        return null;
    }

    private uint SendRequest(ProcessingRequestDto request, CancellationToken cancellationToken)
    {
        requestIdCounter = unchecked(requestIdCounter + 1);
        processingControl.Request.Writer.Write(new ProcessingRequest(requestIdCounter, request), cancellationToken);
        return requestIdCounter;
    }

    private string Query<TResponse>(
        string path, ProcessingRequestDto request, Func<TResponse, string> format,
        int timeoutMs, CancellationToken cancellationToken) where TResponse : ProcessingResponseDto
    {
        var stopwatch = Stopwatch.StartNew();
        uint requestId = SendRequest(request, cancellationToken);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long remainingMs = timeoutMs - stopwatch.ElapsedMilliseconds;
            if (remainingMs <= 0 || !processingControl.Response.Reader.TryRead(out var response, (int)remainingMs, cancellationToken))
            {
                logger.LogError($"{path}? - No response from processing control");
                return ResponseError;
            }
            if (response == null)
            {
                logger.LogError($"{path}? - Invalid response from processing control");
                return ResponseError;
            }
            if (response.RequestId != requestId)
            {
                logger.LogWarning($"Discarding SCPI response for request {response.RequestId}; expected {requestId}");
                continue;
            }
            if (response.Payload is not TResponse typed)
            {
                logger.LogError($"{path}? - Invalid response {response.Payload}; expected {typeof(TResponse).Name}");
                return ResponseError;
            }
            try
            {
                return format(typed);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError(ex, $"{path}? - Invalid response value");
                return ResponseError;
            }
        }
    }

    private string GetDepths()
    {
        var depths = new List<string>();
        for (long count = 1000; count <= settings.MaxCaptureLength; count *= 10)
        {
            foreach (int multiplier in new[] { 1, 2, 5 })
            {
                long depth = count * multiplier;
                if (depth <= settings.MaxCaptureLength)
                    depths.Add(depth.ToString(CultureInfo.InvariantCulture));
            }
        }
        return string.Join(",", depths) + "\n";
    }

    private static uint ParseUInt32(string argument)
    {
        return uint.Parse(argument, CultureInfo.InvariantCulture);
    }

    private static ulong ParseUInt64(string argument)
    {
        return ulong.Parse(argument, CultureInfo.InvariantCulture);
    }

    private static long ParseInt64(string argument)
    {
        return long.Parse(argument, CultureInfo.InvariantCulture);
    }

    private static float ParseFloat(string argument)
    {
        float value = float.Parse(argument, CultureInfo.InvariantCulture);
        if (!float.IsFinite(value))
            throw new FormatException("Expected a finite numeric value.");
        return value;
    }

    private static T ParseChoice<T>(string argument, params (string Name, T Value)[] choices)
    {
        foreach (var choice in choices)
        {
            if (argument.Equals(choice.Name, StringComparison.OrdinalIgnoreCase))
                return choice.Value;
        }
        throw new FormatException($"Expected one of: {string.Join(", ", choices.Select(c => c.Name))}.");
    }

    private static int ParseChannelIndex(string argument)
    {
        for (int index = 0; index < 4; index++)
        {
            if (argument.Equals($"CHAN{index + 1}", StringComparison.OrdinalIgnoreCase) ||
                argument.Equals($"CHANNEL{index + 1}", StringComparison.OrdinalIgnoreCase))
                return index;
        }
        throw new FormatException("Expected CHANnel1 through CHANnel4.");
    }

    private static TriggerChannel ParseTriggerSource(string argument)
    {
        if (argument.Equals("NONE", StringComparison.OrdinalIgnoreCase))
            return TriggerChannel.None;
        if (argument.Equals("EXT", StringComparison.OrdinalIgnoreCase))
            return TriggerChannel.External;
        return (TriggerChannel)(ParseChannelIndex(argument) + 1);
    }

    private static ThunderscopeCoupling ParseCoupling(string argument)
    {
        return ParseChoice(argument, ("DC", ThunderscopeCoupling.DC), ("AC", ThunderscopeCoupling.AC));
    }

    private static ThunderscopeTermination ParseTermination(string argument)
    {
        return ParseChoice(argument, ("1M", ThunderscopeTermination.OneMegaohm), ("50", ThunderscopeTermination.FiftyOhm));
    }

    private static ThunderscopeBandwidth ParseBandwidth(string argument)
    {
        return ParseChoice(argument,
            ("FULL", ThunderscopeBandwidth.BwFull), ("750M", ThunderscopeBandwidth.Bw750M),
            ("650M", ThunderscopeBandwidth.Bw650M), ("350M", ThunderscopeBandwidth.Bw350M),
            ("200M", ThunderscopeBandwidth.Bw200M), ("100M", ThunderscopeBandwidth.Bw100M),
            ("20M", ThunderscopeBandwidth.Bw20M));
    }

    private static string FormatTermination(ThunderscopeTermination termination)
    {
        return termination switch
        {
            ThunderscopeTermination.OneMegaohm => "1M",
            ThunderscopeTermination.FiftyOhm => "50",
            _ => throw new InvalidOperationException($"Unexpected termination: {termination}")
        };
    }

    private static string FormatBandwidth(ThunderscopeBandwidth bandwidth)
    {
        return bandwidth switch
        {
            ThunderscopeBandwidth.BwFull => "FULL",
            ThunderscopeBandwidth.Bw750M => "750M",
            ThunderscopeBandwidth.Bw650M => "650M",
            ThunderscopeBandwidth.Bw350M => "350M",
            ThunderscopeBandwidth.Bw200M => "200M",
            ThunderscopeBandwidth.Bw100M => "100M",
            ThunderscopeBandwidth.Bw20M => "20M",
            _ => throw new InvalidOperationException($"Unexpected bandwidth: {bandwidth}")
        };
    }

    private static string[] SplitArguments(string argument, int count)
    {
        string[] args = argument.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (args.Length != count)
            throw new FormatException($"Expected {count} parameters.");
        return args;
    }

    private record ParsedCommand(string[] Path, bool IsQuery, string Argument)
    {
        public static ParsedCommand Parse(string message)
        {
            message = message.Trim();
            int separator = 0;
            while (separator < message.Length && !char.IsWhiteSpace(message[separator]))
                separator++;

            string header = message[..separator];
            string argument = message[separator..].Trim();
            bool isQuery = header.EndsWith('?');
            if (isQuery)
                header = header[..^1];
            if (header.StartsWith(':'))
                header = header[1..];

            string[] path = header.Split(':');
            if (path.Any(segment => segment.Length == 0) || header.Contains('?') ||
                message.Contains(';') || message.Contains('\r') || message.Contains('\n'))
                throw new FormatException("Expected one command with a complete colon-separated header.");
            return new ParsedCommand(path, isQuery, argument);
        }
    }

    private class CommandContext
    {
        private readonly ScpiHandler handler;
        private readonly string argument;
        private readonly int? channelIndex;

        public string Path { get; }
        public CancellationToken CancellationToken { get; }
        public string Argument
        {
            get
            {
                RequireArgument();
                return argument;
            }
        }
        public int ChannelIndex => channelIndex ?? throw new InvalidOperationException("This handler requires a channel registration.");

        public CommandContext(ScpiHandler handler, string path, string argument, int? channelIndex, CancellationToken cancellationToken)
        {
            this.handler = handler;
            Path = path;
            this.argument = argument;
            this.channelIndex = channelIndex;
            CancellationToken = cancellationToken;
        }

        public string? Action(ProcessingRequestDto request)
        {
            RequireNoArguments();
            return handler.Send(request, CancellationToken);
        }

        public string? Set(ProcessingRequestDto request)
        {
            RequireArgument();
            return handler.Send(request, CancellationToken);
        }

        public string Get<TResponse>(ProcessingRequestDto request, Func<TResponse, string> format, int timeoutMs = ProcessingControlTimeoutMs) where TResponse : ProcessingResponseDto
        {
            RequireNoArguments();
            return handler.Query(Path, request, format, timeoutMs, CancellationToken);
        }

        public string Reply(string response)
        {
            RequireNoArguments();
            return response;
        }

        private void RequireArgument()
        {
            if (argument.Length == 0)
                throw new FormatException("This command requires an argument.");
        }

        private void RequireNoArguments()
        {
            if (argument.Length != 0)
                throw new FormatException("This command does not accept arguments.");
        }
    }

    private record CommandHandler(string Path, int? ChannelIndex, Func<CommandContext, string?> Execute);

    private class CommandNode
    {
        private readonly Dictionary<string, CommandNode> children = new(StringComparer.OrdinalIgnoreCase);
        private CommandHandler? setter;
        private CommandHandler? query;

        public void Register(string path, bool isQuery, CommandHandler handler)
        {
            CommandNode node = this;
            foreach (string segment in path.Split(':'))
            {
                string shortName = string.Concat(segment.Where(c => !char.IsLower(c)));
                if (!node.children.TryGetValue(shortName, out var child))
                {
                    child = new CommandNode();
                    node.children.Add(shortName, child);
                }
                if (node.children.TryGetValue(segment, out var existing) && existing != child)
                    throw new InvalidOperationException($"Conflicting SCPI keyword: {segment}");
                node.children[segment] = child;
                node = child;
            }

            if ((isQuery ? node.query : node.setter) != null)
                throw new InvalidOperationException($"Duplicate SCPI command: {path}");
            if (isQuery)
                node.query = handler;
            else
                node.setter = handler;
        }

        public CommandHandler? Resolve(string[] path, bool isQuery)
        {
            CommandNode node = this;
            foreach (string segment in path)
            {
                if (!node.children.TryGetValue(segment, out var child))
                    return null;
                node = child;
            }
            return isQuery ? node.query : node.setter;
        }
    }

}
