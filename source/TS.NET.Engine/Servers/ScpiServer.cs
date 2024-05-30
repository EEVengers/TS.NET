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

        public ScpiSession(
            TcpServer server,
            ILogger logger,
            BlockingChannelWriter<HardwareRequestDto> hardwareRequestChannel,
            BlockingChannelReader<HardwareResponseDto> hardwareResponseChannel,
            BlockingChannelWriter<ProcessingRequestDto> processingRequestChannel,
            BlockingChannelReader<ProcessingResponseDto> processingResponseChannel) : base(server)
        {
            this.logger = logger;
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

                string? response = ProcessSCPICommand(logger, hardwareRequestChannel, hardwareResponseChannel, processingRequestChannel, processingResponseChannel, message.Trim());

                if (response != null)
                {
                    logger.LogDebug(" -> SCPI reply: '{String}'", response);
                    Send(response);
                }
            }
            //Server.Multicast(message);
            //if (message == "!")
            //    Disconnect();
        }

        protected override void OnError(SocketError error)
        {
            logger.LogDebug($"SCPI session caught an error with code {error}");
        }

        public static string? ProcessSCPICommand(
            ILogger logger,
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

            logger.LogDebug($"SCPI message: {message}");

            if (message.Contains(" "))
            {
                int index = message.IndexOf(" ");
                argument = message.Substring(index + 1);
                command = message.Substring(0, index);
            }
            else if (command.Contains("?"))
            {
                isQuery = true;
                command = message.Substring(0, message.Length - 1);
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

            bool hasArg = argument != null;
            // logger.LogDebug("o:'{String}', q:{bool}, s:'{String?}', c:'{String?}', a:'{String?}'", fullCommand, isQuery, subject, command, argument);

            if (!isQuery)
            {
                if (subject == null)
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
                            if (hasArg)
                            {
                                ulong depth = Convert.ToUInt64(argument);
                                processingRequestChannel.Write(new ProcessingSetDepthDto(depth));
                                logger.LogDebug($"{nameof(ProcessingSetDepthDto)} sent with argument: {depth}");
                            }
                            return null;
                        case "RATE":
                            if (hasArg)
                            {
                                long rate = Convert.ToInt64(argument);
                                processingRequestChannel.Write(new ProcessingSetRateDto(rate));
                                logger.LogDebug($"{nameof(ProcessingSetRateDto)} sent with argument: {rate}");
                            }
                            return null;
                    }
                }
                else if (subject == "TRIG")
                {
                    if (command == "LEV" && hasArg)
                    {
                        double level = Convert.ToDouble(argument);
                        logger.LogDebug($"Set trigger level to {level}V");
                        processingRequestChannel.Write(new ProcessingSetTriggerLevelDto(level));
                        return null;
                    }
                    else if (command == "SOU" && hasArg)
                    {
                        int source = Convert.ToInt32(argument);
                        if (source < 0 || source > 3)
                            source = 0;
                        logger.LogDebug($"Set trigger source to ch {source}");
                        processingRequestChannel.Write(new ProcessingSetTriggerSourceDto((TriggerChannel)(source + 1)));
                        return null;
                    }
                    else if (command == "DELAY" && hasArg)
                    {
                        long delay = Convert.ToInt64(argument);
                        logger.LogDebug($"Set trigger delay to {delay}fs");
                        processingRequestChannel.Write(new ProcessingSetTriggerDelayDto((ulong)delay));
                        return null;
                    }
                    else if (command == "EDGE:DIR" && hasArg)
                    {
                        string dir = argument ?? throw new NullReferenceException();
                        logger.LogDebug($"Set [edge] trigger direction to {dir}");
                        var type = dir.ToUpper() switch {
                            "RISING" => TriggerType.RisingEdge,
                            "FALLING" => TriggerType.FallingEdge,
                            _ => throw new NotImplementedException()
                        };
                        processingRequestChannel.Write(new ProcessingSetTriggerTypeDto(type));
                        return null;
                    }
                }
                else if (subject.Length == 1 && char.IsDigit(subject[0]))
                {
                    int chNum = subject[0] - '0';

                    if (command == "ON" || command == "OFF")
                    {
                        logger.LogDebug($"Set ch {chNum} enabled {command == "ON"}");
                        hardwareRequestChannel.Write(new HardwareSetEnabledRequest(chNum, command == "ON"));
                        return null;
                    }
                    else if (command == "COUP" && hasArg)
                    {
                        string coup = argument ?? throw new NullReferenceException();
                        logger.LogDebug($"Set ch {chNum} coupling to {coup}");
                        hardwareRequestChannel.Write(new HardwareSetCouplingRequest(chNum, coup == "DC1M" ? ThunderscopeCoupling.DC : ThunderscopeCoupling.AC));
                        if(coup.EndsWith("1M"))
                            hardwareRequestChannel.Write(new HardwareSetTerminationRequest(chNum, ThunderscopeTermination.OneMegaohm));
                        else
                            hardwareRequestChannel.Write(new HardwareSetTerminationRequest(chNum, ThunderscopeTermination.FiftyOhm));
                        return null;
                    }
                    else if (command == "OFFS" && hasArg)
                    {
                        double offset = Convert.ToDouble(argument);
                        logger.LogDebug($"Set ch {chNum} offset to {offset}V [Not implemented]");
                        offset = Math.Clamp(offset, -0.5, 0.5);
                        //hardwareRequestChannel.Write(new HardwareSetVoltOffsetRequest(chNum, offset));
                        return null;
                    }
                    else if (command == "RANGE" && hasArg)
                    {
                        double range = Convert.ToDouble(argument);
                        logger.LogDebug($"Set channel {chNum} range to {range}V [Not implemented]");
                        //hardwareRequestChannel.Write(new HardwareSetVoltFullScaleRequest(chNum, range));
                        return null;
                    }
                }
            }
            else
            {
                if (subject == null)
                {
                    switch (command)
                    {
                        case "*IDN":
                            logger.LogDebug("Reply to *IDN? query");
                            return "ThunderScope,(Bridge),NOSERIAL,NOVERSION\n";
                        case "RATES":
                            processingRequestChannel.Write(new ProcessingGetRateRequestDto());
                            if(processingResponseChannel.TryRead(out var response, 100))
                            {
                                switch(response){
                                    case ProcessingGetRateResponseDto hardwareGetRateResponse:
                                        return $"{1000000000000000/hardwareGetRateResponse.SampleRate:F0},\n";
                                    default:
                                        logger.LogWarning($"Did not get correct response to {nameof(ProcessingGetRateRequestDto)}");
                                        return "";
                                }                              
                            }
                            else
                            {
                                logger.LogWarning($"Did not get any response to {nameof(ProcessingGetRateRequestDto)}");
                                return "";
                            }
                        case "DEPTHS":
                            //To do: get maximum channel length from configuration, and generate every 1/2/5 value up to maximum. Perhaps take into account the sample rate to get 1ms/2ms/5ms/10ms/etc windows instead?
                            return "1000,2000,5000,10000,20000,50000,100000,200000,500000,1000000,\n";
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
        private readonly BlockingChannelWriter<HardwareRequestDto> hardwareRequestChannel;
        private readonly BlockingChannelReader<HardwareResponseDto> hardwareResponseChannel;
        private readonly BlockingChannelWriter<ProcessingRequestDto> processingRequestChannel;
        private readonly BlockingChannelReader<ProcessingResponseDto> processingResponseChannel;

        public ScpiServer(ILoggerFactory loggerFactory,
            IPAddress address,
            int port,
            BlockingChannelWriter<HardwareRequestDto> hardwareRequestChannel,
            BlockingChannelReader<HardwareResponseDto> hardwareResponseChannel,
            BlockingChannelWriter<ProcessingRequestDto> processingRequestChannel,
            BlockingChannelReader<ProcessingResponseDto> processingResponseChannel) : base(address, port)
        {
            logger = loggerFactory.CreateLogger(nameof(ScpiServer));
            this.hardwareRequestChannel = hardwareRequestChannel;
            this.hardwareResponseChannel = hardwareResponseChannel;
            this.processingRequestChannel = processingRequestChannel;
            this.processingResponseChannel = processingResponseChannel;
            logger.LogDebug("Started");
        }

        protected override TcpSession CreateSession()
        {
            return new ScpiSession(this, logger, hardwareRequestChannel, hardwareResponseChannel, processingRequestChannel, processingResponseChannel);
        }

        protected override void OnError(SocketError error)
        {
            logger.LogDebug($"SCPI server caught an error with code {error}");
        }
    }
}
