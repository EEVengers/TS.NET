using System;
using System.Collections.Concurrent;

namespace TS.NET
{
    public class BlockingChannel<T>
    {
        public BlockingChannelReader<T> Reader { get; private set; }
        public BlockingChannelWriter<T> Writer { get; private set; }

        private readonly BlockingCollection<T> collection;

        public BlockingChannel()
        {
            collection = new BlockingCollection<T>();
            Reader = new BlockingChannelReader<T>(collection);
            Writer = new BlockingChannelWriter<T>(collection);
        }

        public BlockingChannel(int boundedCapacity)
        {
            collection = new BlockingCollection<T>(boundedCapacity);
            Reader = new BlockingChannelReader<T>(collection);
            Writer = new BlockingChannelWriter<T>(collection);
        }
    }

    // Pair of channels, used for reuse of large objects/memory
    public class BlockingPool<T>
    {
        public BlockingChannel<T> Source { get; private set; }
        public BlockingChannel<T> Return { get; private set; }

        public BlockingPool()
        {
            Source = new BlockingChannel<T>();
            Return = new BlockingChannel<T>();
        }

        public BlockingPool(int boundedCapacity)
        {
            Source = new BlockingChannel<T>(boundedCapacity);
            Return = new BlockingChannel<T>(boundedCapacity);
        }
    }

    // Pair of channels, used for request/response
    public class BlockingRequestResponse<T1,T2>
    {
        public BlockingChannel<T1> Request { get; private set; }
        public BlockingChannel<T2> Response { get; private set; }

        public BlockingRequestResponse()
        {
            Request = new BlockingChannel<T1>();
            Response = new BlockingChannel<T2>();
        }

        public BlockingRequestResponse(int boundedCapacity)
        {
            Request = new BlockingChannel<T1>(boundedCapacity);
            Response = new BlockingChannel<T2>(boundedCapacity);
        }
    }

    public class BlockingChannelReader<T>
    {
        private readonly BlockingCollection<T> collection;
        internal BlockingChannelReader(BlockingCollection<T> collection)
        {
            ArgumentNullException.ThrowIfNull(collection);
            this.collection = collection;
        }

        public T Read()
        {
            return collection.Take();
        }

        public T Read(CancellationToken cancellationToken)
        {
            return collection.Take(cancellationToken);
        }

        public bool TryRead(out T? item)
        {
            return collection.TryTake(out item);
        }

        public bool TryRead(out T? item, int millisecondsTimeout)
        {
            return collection.TryTake(out item, millisecondsTimeout);
        }

        public bool TryRead(out T? item, CancellationToken cancellationToken)
        {
            return collection.TryTake(out item, -1, cancellationToken);
        }

        public bool TryRead(out T? item, int millisecondsTimeout, CancellationToken cancellationToken)
        {
            return collection.TryTake(out item, millisecondsTimeout, cancellationToken);
        }

        public bool TryRead(out T? item, TimeSpan timeout)
        {
            return collection.TryTake(out item, timeout);
        }

        public int PeekAvailable()
        {
            return collection.Count;
        }
    }

    public class BlockingChannelWriter<T>
    {
        private readonly BlockingCollection<T> collection;

        internal BlockingChannelWriter(BlockingCollection<T> collection)
        {
            ArgumentNullException.ThrowIfNull(collection);
            this.collection = collection;
        }

        public void Write(T item)
        {
            collection.Add(item);
        }

        public void Write(T item, CancellationToken cancellationToken)
        {
            collection.Add(item, cancellationToken);
        }

        public bool TryWrite(T item, int millisecondsTimeout, CancellationToken cancellationToken)
        {
            return collection.TryAdd(item, millisecondsTimeout, cancellationToken);
        }

        public bool TryWrite(T item, int millisecondsTimeout)
        {
            return collection.TryAdd(item, millisecondsTimeout);
        }

        public bool TryWrite(T item, TimeSpan timeout)
        {
            return collection.TryAdd(item, timeout);
        }

        public bool TryWrite(T item)
        {
            return collection.TryAdd(item);
        }
    }
}
