namespace TS.NET.Driver.Simulation
{
    public class Thunderscope : IThunderscope
    {
        private DateTimeOffset startTimestamp;
        private double totalTimeSec;

        private Memory<sbyte> oneCycle;
        private float scaleRelativeToFull = 1.0f;
        private float sampleRateHz = 1e9f;
        private float frequencyHz = 0.99e6f;
        private int nextIterationCopy = 0;
        private bool running = false;

        public ThunderscopeChannelFrontend GetChannelFrontend(int channelIndex)
        {
            return new ThunderscopeChannelFrontend();
        }

        public ThunderscopeHardwareConfig GetConfiguration()
        {
            var config = new ThunderscopeHardwareConfig()
            {
                AdcChannelMode = AdcChannelMode.Single,
                SampleRateHz = (ulong)sampleRateHz,
                EnabledChannels = 0x01
            };
            config.Frontend[0].ActualVoltFullScale = 1;
            config.Frontend[1].ActualVoltFullScale = 1;
            config.Frontend[2].ActualVoltFullScale = 1;
            config.Frontend[3].ActualVoltFullScale = 1;
            return config;
        }

        public void Read(ThunderscopeMemory data, CancellationToken cancellationToken)
        {
            // To do: maintain phase counter and then generate or use pre-computed waveform in real-time so frequency can be configurable.
            var dataSpan = data.DataSpanI8;
            var copyLength = dataSpan.Length;

            if (nextIterationCopy > 0)
            {
                oneCycle.Span.Slice(oneCycle.Length - nextIterationCopy, nextIterationCopy).CopyTo(dataSpan.Slice(0, nextIterationCopy));
                copyLength -= nextIterationCopy;
            }

            var fullBufferCopies = copyLength / oneCycle.Length;
            for (int i = 0; i < fullBufferCopies; i++)
            {
                oneCycle.Span.CopyTo(dataSpan.Slice(nextIterationCopy + (i * oneCycle.Length), oneCycle.Length));
            }

            var remainingCopy = copyLength % oneCycle.Length;
            oneCycle.Span.Slice(0, remainingCopy).CopyTo(dataSpan.Slice(nextIterationCopy + (fullBufferCopies * oneCycle.Length), remainingCopy));

            nextIterationCopy = oneCycle.Length - remainingCopy;

            var duration = DateTime.UtcNow - startTimestamp;
            var sleepTime = totalTimeSec - duration.TotalSeconds;
            if (sleepTime < 0)
                sleepTime = 0;
            Thread.Sleep((int)(sleepTime * 1000));
            totalTimeSec += dataSpan.Length / sampleRateHz;
        }

        public bool TryRead(ThunderscopeMemory data, CancellationToken cancellationToken)
        {
            if (!running)
                return false;
            Read(data, cancellationToken);
            return true;
        }

        public void SetChannelFrontend(int channelIndex, ThunderscopeChannelFrontend channel)
        {
            //throw new NotImplementedException();
        }

        public void SetChannelCalibration(int channelIndex, ThunderscopeChannelCalibration channelCalibration)
        {
            throw new NotImplementedException();
        }

        public void SetChannelEnable(int channelIndex, bool enabled)
        {
            //throw new NotImplementedException();
        }

        public void Start()
        {
            startTimestamp = DateTimeOffset.UtcNow;
            totalTimeSec = 0;

            // Calculate samples for 500 microseconds at the current sample rate
            var samplesFor500us = (int)(sampleRateHz * 500e-6f); // 500 microseconds in samples
            var totalPatternSamples = samplesFor500us * 2; // 500us sine + 500us zero = 1000us total pattern

            var scale = (scaleRelativeToFull * (sbyte.MaxValue - sbyte.MinValue-20)) * 0.5f;
            var angularFrequency = (2.0f * MathF.PI * frequencyHz);
            
            oneCycle = new sbyte[totalPatternSamples];
            
            // Generate sine wave for first 500us
            for (int i = 0; i < samplesFor500us; i++)
            {
                float time = (i / sampleRateHz);
                float sineValue = MathF.Sin(angularFrequency * time);
                int scaledValue = (int)(sineValue * scale);
                oneCycle.Span[i] = (sbyte)scaledValue;
            }
            
            // Generate zero signal for next 500us
            for (int i = samplesFor500us; i < totalPatternSamples; i++)
            {
                oneCycle.Span[i] = 0;
            }

            running = true;
        }

        public void Stop()
        {
            running = false;
        }

        public ThunderscopeChannelCalibration GetChannelCalibration(int channelIndex)
        {
            throw new NotImplementedException();
        }

        public void SetRate(ulong sampleRateHz)
        {
        }

        public void SetResolution(AdcResolution resolution)
        {

        }
    }
}
