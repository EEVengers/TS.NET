using Cloudtoid.Interprocess;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TS.NET
{
    public static class InterprocessExtensions
    {
        public static bool TryEnqueue<T>(this IPublisher publisher, T data, Span<byte> buffer)
        {
            var dtoType = typeof(T);
            Span<byte> dtoName = Encoding.UTF8.GetBytes(dtoType.Name);              // Later improvement: cache type names
            if (dtoName.Length > 255)
                throw new InvalidDataException("DTO name too long");

            Span<byte> payload = MessagePackSerializer.Serialize(data);

            var totalLength = 1 + dtoName.Length + payload.Length;
            if (buffer.Length < totalLength)
                throw new InvalidDataException("Buffer too small");

            // Now pack data into wire format
            Span<byte> message = buffer.Slice(0, totalLength);
            message[0] = (byte)dtoName.Length;
            dtoName.CopyTo(message.Slice(1));
            payload.CopyTo(message.Slice(1 + dtoName.Length));
            return publisher.TryEnqueue(message);
        }

        public static bool TryDequeue(this ISubscriber subscriber, CancellationToken cancellation, out string dtoName, out ReadOnlyMemory<byte> payload)
        {
            if (subscriber.TryDequeue(cancellation, out ReadOnlyMemory<byte> message))
            {
                var dtoNameLength = message.Span[0];
                dtoName = Encoding.UTF8.GetString(message.Slice(1, dtoNameLength).Span);
                payload = message.Slice(1 + dtoNameLength);
                return true;
            }
            else
            {
                dtoName = default;
                payload = default;
                return false;
            }
        }

        public static T Deserialise<T>(this ReadOnlyMemory<byte> payload)
        {
            return MessagePackSerializer.Deserialize<T>(payload);
        }
    }
}