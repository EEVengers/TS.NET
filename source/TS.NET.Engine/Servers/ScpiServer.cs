using Microsoft.Extensions.Logging;
using NetCoreServer;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TS.NET.Engine
{
    internal class ScpiSession : TcpSession
    {
        private readonly ILogger logger;
        private readonly BlockingChannelWriter<HardwareRequestDto> hardwareRequestChannel;
        private readonly BlockingChannelReader<HardwareResponseDto> hardwareResponseChannel;
        private readonly BlockingChannelWriter<ProcessingRequestDto> processingRequestChannel;
        private readonly BlockingChannelReader<ProcessingResponseDto> processingResponseChannel;
        private readonly ThunderscopeSettings settings;

        public ScpiSession(
            TcpServer server,
            ILogger logger,
            ThunderscopeSettings settings,
            BlockingChannelWriter<HardwareRequestDto> hardwareRequestChannel,
            BlockingChannelReader<HardwareResponseDto> hardwareResponseChannel,
            BlockingChannelWriter<ProcessingRequestDto> processingRequestChannel,
            BlockingChannelReader<ProcessingResponseDto> processingResponseChannel) : base(server)
        {
            this.logger = logger;
            this.settings = settings;
            this.hardwareRequestChannel = hardwareRequestChannel;
            this.hardwareResponseChannel = hardwareResponseChannel;
            this.processingRequestChannel = processingRequestChannel;
            this.processingResponseChannel = processingResponseChannel;
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

                string? response = ProcessSCPICommand(logger, settings, hardwareRequestChannel, hardwareResponseChannel, processingRequestChannel, processingResponseChannel, message.Trim());

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
            BlockingChannelWriter<HardwareRequestDto> hardwareRequestChannel,
            BlockingChannelReader<HardwareResponseDto> hardwareResponseChannel,
            BlockingChannelWriter<ProcessingRequestDto> processingRequestChannel,
            BlockingChannelReader<ProcessingResponseDto> processingResponseChannel,
            string message)
        {
            string? argument = null;
            string? subject = null;
            string command = message;
            bool isQuery = false;

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
                                    processingRequestChannel.Write(new ProcessingRunDto());
                                    logger.LogDebug($"{nameof(ProcessingRunDto)} sent");
                                    return null;
                                case "STOP":
                                    processingRequestChannel.Write(new ProcessingStopDto());
                                    logger.LogDebug($"{nameof(ProcessingStopDto)} sent");
                                    return null;
                                case "FORCE":
                                    processingRequestChannel.Write(new ProcessingForceTriggerDto());
                                    logger.LogDebug($"{nameof(ProcessingForceTriggerDto)} sent");
                                    return null;
                                case "SINGLE":
                                    processingRequestChannel.Write(new ProcessingSetTriggerModeDto(TriggerMode.Single));
                                    logger.LogDebug($"{nameof(ProcessingSetTriggerModeDto)} sent");
                                    return null;
                                case "NORMAL":
                                    processingRequestChannel.Write(new ProcessingSetTriggerModeDto(TriggerMode.Normal));
                                    logger.LogDebug($"{nameof(ProcessingSetTriggerModeDto)} sent");
                                    return null;
                                case "AUTO":
                                    processingRequestChannel.Write(new ProcessingSetTriggerModeDto(TriggerMode.Auto));
                                    logger.LogDebug($"{nameof(ProcessingSetTriggerModeDto)} sent");
                                    return null;
                                case "STREAM":
                                    processingRequestChannel.Write(new ProcessingSetTriggerModeDto(TriggerMode.Stream));
                                    logger.LogDebug($"{nameof(ProcessingSetTriggerModeDto)} sent");
                                    return null;
                                case "DEPTH":
                                    if (argument != null)
                                    {
                                        ulong depth = Convert.ToUInt64(argument);
                                        processingRequestChannel.Write(new ProcessingSetDepthDto(depth));
                                        logger.LogDebug($"{nameof(ProcessingSetDepthDto)} sent with argument: {depth}");
                                    }
                                    return null;
                                case "RATE":
                                    if (argument != null)
                                    {
                                        ulong rate = Convert.ToUInt64(argument);
                                        hardwareRequestChannel.Write(new HardwareSetRateRequest(rate));
                                        logger.LogDebug($"{nameof(HardwareSetRateRequest)} sent with argument: {rate}");
                                    }
                                    return null;
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
                                case var _ when command.StartsWith("LEV") && argument != null:
                                    {
                                        // TRIGger:LEVel <arg>
                                        // TRIG:LEV <arg>
                                        double level = Convert.ToDouble(argument);
                                        logger.LogDebug($"Set trigger level to {level}V");
                                        processingRequestChannel.Write(new ProcessingSetTriggerLevelDto(level));
                                        return null;
                                    }
                                case var _ when command.StartsWith("SOU") && argument != null:
                                    {
                                        // TRIGger:SOUrce <arg>
                                        // TRIG:SOU <arg>
                                        if (!char.IsDigit(argument[^1]))
                                        {
                                            logger.LogWarning($"Trigger source argument not valid");
                                            break;
                                        }
                                        int source = Convert.ToInt32(argument.ToArray()[^1]) - '0';
                                        if (source < 1 || source > 4)
                                            source = 1;
                                        logger.LogDebug($"Set trigger source to ch {source}");
                                        processingRequestChannel.Write(new ProcessingSetTriggerSourceDto((TriggerChannel)source));
                                        return null;
                                    }
                                case var _ when command.StartsWith("DEL") && argument != null:
                                    {
                                        // TRIGger:DELay <arg>
                                        // TRIG:DEL <arg>
                                        long delay = Convert.ToInt64(argument);
                                        logger.LogDebug($"Set trigger delay to {delay}fs");
                                        processingRequestChannel.Write(new ProcessingSetTriggerDelayDto((ulong)delay));
                                        return null;
                                    }
                                case var _ when command.StartsWith("EDGE") && argument != null:
                                    {
                                        // TRIGger:EDGE:SLOPe <arg>
                                        // TRIG:EDGE:SLOP <arg>
                                        string dir = argument ?? throw new NullReferenceException();
                                        logger.LogDebug($"Set [edge] trigger direction to {dir}");
                                        var type = dir.ToUpper() switch
                                        {
                                            "RISING" => TriggerType.RisingEdge,
                                            "FALLING" => TriggerType.FallingEdge,
                                            "ANY" => TriggerType.AnyEdge,
                                            _ => throw new NotImplementedException()
                                        };
                                        processingRequestChannel.Write(new ProcessingSetTriggerTypeDto(type));
                                        return null;
                                    }
                                    //case var _ when command.StartsWith("HOLD") && argument != null:
                                    //    {
                                    //        // TRIGger:HOLDoff:MODE <OFF|TIME>
                                    //        // TRIGger:HOLDoff:TIME <arg>
                                    //    }
                            }
                            break;
                        }
                    case var _ when char.IsDigit(subject[0]):    // Maintain backwards compatibility, remove later
                    case var _ when subject.StartsWith("CHAN") && char.IsDigit(subject[^1]):
                        {
                            // CHANnel1:
                            // CHAN1:
                            int channelNumber = subject[^1] - '0';
                            if ((channelNumber < 1) || (channelNumber > 4))
                            {
                                logger.LogWarning("Channel index out of range, allowable values are 1 - 4");
                                return null;
                            }
                            int channelIndex = channelNumber - 1;
                            switch (command)
                            {
                                case "ON" or "OFF":
                                    {
                                        hardwareRequestChannel.Write(new HardwareSetEnabledRequest(channelIndex, command == "ON"));
                                        return null;
                                    }
                                case var _ when command.StartsWith("BAND") && argument != null:
                                    {
                                        // CHANnel1:BANDwidth <arg>
                                        // CHAN1:BAND <arg>
                                        ThunderscopeBandwidth? thunderscopeBandwidth = argument switch
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
                                            logger.LogWarning("Bandwidth argument not recognised");
                                            break;
                                        }
                                        hardwareRequestChannel.Write(new HardwareSetBandwidthRequest(channelIndex, (ThunderscopeBandwidth)thunderscopeBandwidth));
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
                                            logger.LogWarning("Coupling argument not recognised");
                                            break;
                                        }
                                        hardwareRequestChannel.Write(new HardwareSetCouplingRequest(channelIndex, (ThunderscopeCoupling)thunderscopeCoupling));
                                        return null;
                                    }
                                case var _ when command.StartsWith("TERM") && argument != null:
                                    {
                                        // CHANnel1:TERMination <arg>
                                        // CHAN1:TERM <arg>
                                        ThunderscopeTermination? thunderscopeTermination = argument switch
                                        {
                                            "1M" => ThunderscopeTermination.OneMegaohm,
                                            "50" => ThunderscopeTermination.FiftyOhm,
                                            _ => null
                                        };
                                        if (thunderscopeTermination == null)
                                        {
                                            logger.LogWarning("Termination argument not recognised");
                                            break;
                                        }
                                        hardwareRequestChannel.Write(new HardwareSetTerminationRequest(channelIndex, (ThunderscopeTermination)thunderscopeTermination));
                                        return null;
                                    }
                                case var _ when command.StartsWith("OFFS") && argument != null:
                                    {
                                        // CHANnel1:OFFSet <arg>
                                        // CHAN1:OFFS <arg>
                                        double offset = Convert.ToDouble(argument);
                                        offset = Math.Clamp(offset, -50, 50);     // Change to final values later
                                        hardwareRequestChannel.Write(new HardwareSetVoltOffsetRequest(channelIndex, offset));
                                        return null;
                                    }
                                case var _ when command.StartsWith("RANG") && argument != null:
                                    {
                                        double range = Convert.ToDouble(argument);
                                        range = Math.Clamp(range, -50, 50);       // Change to final values later
                                        hardwareRequestChannel.Write(new HardwareSetVoltFullScaleRequest(channelIndex, range));
                                        return null;
                                    }
                                default:
                                    break;  // Will log a warning at the end of the handler
                            }
                            break;
                        }
                    case var _ when subject.StartsWith("CAL"):
                        {
                            // :CALibration
                            // :CAL
                            switch (command)
                            {
                                case var _ when command.StartsWith("OFFSET:GAIN:LOW") && argument != null:
                                    {
                                        var args = argument.Split(' ');
                                        if (!char.IsDigit(args[0][^1]))
                                        {
                                            logger.LogWarning("Parameter not valid");
                                            break;
                                        }
                                        int channelNumber = args[0][^1] - '0';
                                        if ((channelNumber < 1) || (channelNumber > 4))
                                        {
                                            logger.LogWarning("Channel index out of range, allowable values are 1 - 4");
                                            return null;
                                        }
                                        int channelIndex = channelNumber - 1;
                                        double voltage = Convert.ToDouble(args[1]);
                                        voltage = Math.Clamp(voltage, 0, 5);
                                        hardwareRequestChannel.Write(new HardwareSetOffsetVoltageLowGainRequest(channelIndex, voltage));
                                        return null;
                                    }
                                case var _ when command.StartsWith("OFFSET:GAIN:HIGH") && argument != null:
                                    {
                                        var args = argument.Split(' ');
                                        if (!char.IsDigit(args[0][^1]))
                                        {
                                            logger.LogWarning("Parameter not valid");
                                            break;
                                        }
                                        int channelNumber = args[0][^1] - '0';
                                        if ((channelNumber < 1) || (channelNumber > 4))
                                        {
                                            logger.LogWarning("Channel index out of range, allowable values are 1 - 4");
                                            return null;
                                        }
                                        int channelIndex = channelNumber - 1;
                                        double voltage = Convert.ToDouble(args[1]);
                                        voltage = Math.Clamp(voltage, 0, 5);
                                        hardwareRequestChannel.Write(new HardwareSetOffsetVoltageHighGainRequest(channelIndex, voltage));
                                        return null;
                                    }
                                case var _ when command.StartsWith("OVERRIDE:PGA:CONFIG") && argument != null:
                                    {
                                        var args = argument.Split(' ');
                                        if (!char.IsDigit(args[0][^1]))
                                        {
                                            logger.LogWarning("Parameter not valid");
                                            break;
                                        }
                                        int channelNumber = args[0][^1] - '0';
                                        if ((channelNumber < 1) || (channelNumber > 4))
                                        {
                                            logger.LogWarning("Channel index out of range, allowable values are 1 - 4");
                                            return null;
                                        }
                                        int channelIndex = channelNumber - 1;
                                        ushort pgaConfigWord = ushort.Parse(args[1]);
                                        hardwareRequestChannel.Write(new HardwareSetPgaConfigWordOverrideRequest(channelIndex, pgaConfigWord));
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
                            return "ThunderScope,(Bridge),NOSERIAL,NOVERSION\n";
                        case "RATES?":
                            {
                                hardwareRequestChannel.Write(new HardwareGetRatesRequest());
                                if (hardwareResponseChannel.TryRead(out var response, 100))
                                {
                                    switch (response)
                                    {
                                        case HardwareGetRatesResponse hardwareGetRatesResponse:
                                            return $"{string.Join(",", hardwareGetRatesResponse.SampleRatesHz)},\n";
                                        default:
                                            logger.LogError($"RATES? - Invalid response from {nameof(hardwareResponseChannel)}");
                                            return "Error: Invalid response from hardware.\n";
                                    }
                                }
                                logger.LogError($"RATES? - No response from {nameof(hardwareResponseChannel)}");
                                return "Error: No response from hardware.\n";
                            }
                        case "RATE?":
                            {
                                hardwareRequestChannel.Write(new HardwareGetRateRequest());
                                if (hardwareResponseChannel.TryRead(out var response, 100))
                                {
                                    switch (response)
                                    {
                                        case HardwareGetRateResponse hardwareGetRateResponse:
                                            return $"{hardwareGetRateResponse.SampleRateHz}\n";
                                        default:
                                            logger.LogError($"RATES? - Invalid response from {nameof(hardwareResponseChannel)}");
                                            return "Error: Invalid response from hardware.\n";
                                    }
                                }
                                logger.LogError($"RATES? - No response from {nameof(hardwareResponseChannel)}");
                                return "Error: No response from hardware.\n";
                            }
                        case "DEPTHS?":
                            List<string> depths = [];
                            int baseCount = 1000;
                            while (true)
                            {
                                if (baseCount <= settings.MaxChannelDataLength)
                                    depths.Add($"{baseCount}");
                                if (baseCount * 2 <= settings.MaxChannelDataLength)
                                    depths.Add($"{baseCount * 2}");
                                if (baseCount * 5 <= settings.MaxChannelDataLength)
                                    depths.Add($"{baseCount * 5}");
                                baseCount *= 10;
                                if (baseCount > settings.MaxChannelDataLength)
                                    break;
                            }
                            // Perhaps take into account the sample rate to get 1ms/2ms/5ms/10ms/etc windows instead?
                            return $"{string.Join(",", depths)},\n";
                    }
                }
            }

            logger.LogWarning("Unknown SCPI Operation: {String}", message);
            return null;
        }
    }

    class ScpiServer : TcpServer
    {
        private readonly ILogger logger;
        private readonly ThunderscopeSettings settings;
        private readonly BlockingChannelWriter<HardwareRequestDto> hardwareRequestChannel;
        private readonly BlockingChannelReader<HardwareResponseDto> hardwareResponseChannel;
        private readonly BlockingChannelWriter<ProcessingRequestDto> processingRequestChannel;
        private readonly BlockingChannelReader<ProcessingResponseDto> processingResponseChannel;

        public ScpiServer(ILoggerFactory loggerFactory,
            ThunderscopeSettings settings,
            IPAddress address,
            int port,
            BlockingChannelWriter<HardwareRequestDto> hardwareRequestChannel,
            BlockingChannelReader<HardwareResponseDto> hardwareResponseChannel,
            BlockingChannelWriter<ProcessingRequestDto> processingRequestChannel,
            BlockingChannelReader<ProcessingResponseDto> processingResponseChannel) : base(address, port)
        {
            logger = loggerFactory.CreateLogger(nameof(ScpiServer));
            this.settings = settings;
            this.hardwareRequestChannel = hardwareRequestChannel;
            this.hardwareResponseChannel = hardwareResponseChannel;
            this.processingRequestChannel = processingRequestChannel;
            this.processingResponseChannel = processingResponseChannel;
            logger.LogDebug("Started");
        }

        protected override TcpSession CreateSession()
        {
            return new ScpiSession(this, logger, settings, hardwareRequestChannel, hardwareResponseChannel, processingRequestChannel, processingResponseChannel);
        }

        protected override void OnError(SocketError error)
        {
            logger.LogDebug($"SCPI server caught an error with code {error}");
        }
    }
}
