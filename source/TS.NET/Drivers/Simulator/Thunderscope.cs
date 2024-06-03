namespace TS.NET.Driver.Simulator
{
    public class Thunderscope : IThunderscope
    {
        private Memory<sbyte> waveformBytes;
        private DateTimeOffset startTimestamp;
        private double totalTime;

        public ThunderscopeChannel GetChannel(int channelIndex)
        {
            return new ThunderscopeChannel();
        }

        public ThunderscopeHardwareConfig GetConfiguration()
        {
            var config = new ThunderscopeHardwareConfig() { AdcChannelMode = AdcChannelMode.Quad };
            config.Channels[0].ActualVoltFullScale = 1;
            config.Channels[1].ActualVoltFullScale = 1;
            config.Channels[2].ActualVoltFullScale = 1;
            config.Channels[3].ActualVoltFullScale = 1;
            return config;
        }

        public void Read(ThunderscopeMemory data, CancellationToken cancellationToken)
        {
            // To do: maintain phase counter and then generate or use pre-computed waveform in real-time so frequency can be configurable.

            waveformBytes.Span.CopyTo(data.SpanI8);
            totalTime += 8.388608;

            var duration = DateTime.UtcNow - startTimestamp;
            var sleepTime = totalTime - duration.TotalMilliseconds;
            if (sleepTime < 0)
                sleepTime = 0;
            Thread.Sleep((int)sleepTime);
        }

        public void SetChannel(int channelIndex, ThunderscopeChannel channel)
        {
            throw new NotImplementedException();
        }

        public void SetChannelEnable(int channelIndex, bool enabled)
        {
            throw new NotImplementedException();
        }

        public void Start()
        {           
            uint byteBufferSize = ThunderscopeMemory.Length;
            double samplingRate = 1000000000;
            double frequency = 476.837158203125; //976562;
            waveformBytes = new sbyte[byteBufferSize];
            Waveforms.FourChannelSineI8(waveformBytes.Span, samplingRate, frequency);

            startTimestamp = DateTimeOffset.UtcNow;
            totalTime = 0;
        }

        public void Stop()
        {
        }
    }
}
