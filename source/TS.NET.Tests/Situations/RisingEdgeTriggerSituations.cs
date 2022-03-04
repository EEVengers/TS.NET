using System;

namespace TS.NET.Tests
{
    public class TriggerSituation
    {
        public byte TriggerLevel { get; set; }
        public byte ArmLevel { get; set; }
        public uint HoldoffSamples { get; set; }
        public Memory<byte> Input { get; set; }
        public Memory<ulong> ExpectedTriggers { get; set; }
    }

    public class RisingEdgeTriggerSituations
    {
        // Trigger at [0] and [^1]
        public static TriggerSituation SituationA()
        {
            Memory<byte> inputMemory = new byte[8000000];
            var data = inputMemory.Span;

            data[0] = 127;
            data.Slice(1, 3999999).Fill(255);
            data.Slice(4000000, 4000000).Fill(0);
            data[7999999] = 127;

            Memory<ulong> expectedTriggersMemory = new ulong[8000000 / 64];
            var result = expectedTriggersMemory.Span;
            result[0] = 0x01;
            result[^1] = 0x8000000000000000;

            return new() { TriggerLevel = 127, ArmLevel = 117, HoldoffSamples = 1000, Input = inputMemory, ExpectedTriggers = expectedTriggersMemory };
        }
    }
}