using Cloudtoid.Interprocess;

namespace TS.NET.Driver.Simulator
{
    public class Thunderscope : IThunderscope
    {
        private readonly ISubscriber subscriber;

        public Thunderscope()
        {
            var factory = new QueueFactory();
            var options = new QueueOptions(queueName: "ThunderScope.Simulator", bytesCapacity: 4 * ThunderscopeMemory.Length);
            subscriber = factory.CreateSubscriber(options);
        }

        public ThunderscopeChannel GetChannel(int channelIndex)
        {
            return new ThunderscopeChannel();
        }

        public ThunderscopeConfiguration GetConfiguration()
        {
            return new ThunderscopeConfiguration() { AdcChannelMode = AdcChannelMode.Quad };
        }

        public void Read(ThunderscopeMemory data, CancellationToken cancellationToken)
        {
            var memory = subscriber.Dequeue(cancellationToken);
            memory.Span.CopyTo(data.SpanU8);
        }

        public void SetChannel(ThunderscopeChannel channel, int channelIndex)
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }
    }
}
