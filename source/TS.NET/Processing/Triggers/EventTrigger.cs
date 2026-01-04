namespace TS.NET;

public class EventTrigger : IEventTrigger
{
    enum TriggerState { Unarmed, InCapture, InHoldoff }
    private TriggerState triggerState = TriggerState.Unarmed;

    private long windowWidth;

    private long captureSamples;
    private long captureRemaining;

    private long holdoffSamples;
    private long holdoffRemaining;

    Queue<ulong> eventQueue = new Queue<ulong>();

    public EventTrigger()
    {
        SetHorizontal(1000000, 0, 0);
    }

    public void SetHorizontal(long windowWidth, long windowTriggerPosition, long additionalHoldoff)
    {
        if (windowWidth < 1000)
            throw new ArgumentException("windowWidth cannot be less than 1000");
        if (windowTriggerPosition > windowWidth - 1)
            windowTriggerPosition = windowWidth - 1;

        this.windowWidth = windowWidth;

        captureSamples = windowWidth - windowTriggerPosition;
        captureRemaining = 0;

        holdoffSamples = windowWidth - captureSamples + additionalHoldoff;
        holdoffRemaining = windowWidth - captureSamples;

        if (holdoffRemaining != 0)
            triggerState = TriggerState.InHoldoff;
        else
            triggerState = TriggerState.Unarmed;
    }

    public void EnqueueEvent(ulong sampleIndex)
    {
        eventQueue.Enqueue(sampleIndex);
    }

    public void Process(int inputLength, ulong sampleStartIndex, int acquisitionSamplesInBuffer, ref EventTriggerResults results)
    {
        ulong chunkStart = sampleStartIndex;
        ulong chunkEnd = sampleStartIndex + (ulong)inputLength;
        results.CaptureEndCount = 0;
        int i = 0;

        // This is similar in style to the other triggers for consistency, but it could be simplified.
        while (i < inputLength)
        {
            switch (triggerState)
            {
                case TriggerState.Unarmed:
                    {

                        // Empty stale events out of queue.
                        // This does limit the scope of triggers to current & future chunks,
                        // when in reality there's a whole acquisition buffer available.
                        // Maybe re-evaluate in future.
                        while (eventQueue.TryPeek(out var potentiallyStaleEvent) && potentiallyStaleEvent < chunkStart)
                        {
                            Console.WriteLine($"Dropping {potentiallyStaleEvent} < {chunkStart}");
                            eventQueue.Dequeue();
                        }

                        // Only allow capture events once at least windowWidth samples have passed
                        if (acquisitionSamplesInBuffer < windowWidth)
                        {
                            i = inputLength;
                            break;
                        }

                        if (eventQueue.TryPeek(out var eventIndex) && eventIndex >= chunkStart && eventIndex < chunkEnd)
                        {
                            eventQueue.Dequeue();
                            triggerState = TriggerState.InCapture;
                            captureRemaining = captureSamples;

                            var offset = (int)(eventIndex - chunkStart);
                            if (offset >= 0 && offset < inputLength)
                                i = offset;
                        }
                        else
                        {
                            i = inputLength;
                        }
                    }
                    break;

                case TriggerState.InCapture:
                    {
                        int remainingSamples = inputLength - i;
                        if (remainingSamples > captureRemaining)
                        {
                            i += (int)captureRemaining;
                            captureRemaining = 0;
                        }
                        else
                        {
                            captureRemaining -= remainingSamples;
                            i = inputLength;
                        }

                        if (captureRemaining == 0)
                        {
                            results.CaptureEndIndices[results.CaptureEndCount++] = sampleStartIndex + (ulong)i;
                            if (holdoffSamples > 0)
                            {
                                triggerState = TriggerState.InHoldoff;
                                holdoffRemaining = holdoffSamples;
                            }
                            else
                            {
                                triggerState = TriggerState.Unarmed;
                            }
                        }
                    }
                    break;

                case TriggerState.InHoldoff:
                    {
                        int remainingSamples = inputLength - i;
                        if (remainingSamples > holdoffRemaining)
                        {
                            i += (int)holdoffRemaining;
                            holdoffRemaining = 0;
                        }
                        else
                        {
                            holdoffRemaining -= remainingSamples;
                            i = inputLength;
                        }
                        if (holdoffRemaining == 0)
                        {
                            triggerState = TriggerState.Unarmed;
                        }
                    }
                    break;
            }
        }
    }
}