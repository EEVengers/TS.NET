using System;

namespace TS.NET.Tests
{
    internal class BurstTriggerSituation
    {
        public BurstTriggerParameters Parameters;
        public uint WindowWidth;
        public uint WindowTriggerPosition;
        public uint AdditionalHoldoff;

        public int ChunkSize;
        public int ChunkCount;

        public Memory<sbyte> Input;
        public Memory<uint> ExpectedWindowEndIndices;
    }

    internal class BurstTriggerSituations
    {
        /// <summary>
        /// 100 samples idle, 100 sample wide positive pulse
        /// </summary>
        public static BurstTriggerSituation SituationA()
        {
            var situation = new BurstTriggerSituation()
            {
                Parameters = new BurstTriggerParameters(WindowHighLevel: 20, WindowLowLevel: -20, 1000),
                WindowWidth = 10000,
                WindowTriggerPosition = 0,
                AdditionalHoldoff = 0,

                ChunkSize = 8388608,
                ChunkCount = 1
            };
            situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
            situation.ExpectedWindowEndIndices = new uint[1];
            situation.Input.Span.Fill(sbyte.MaxValue);
            situation.Input.Span.Slice(100, 100).Fill(0);   // Should be ignored
            situation.Input.Span.Slice(2000, 100).Fill(0);
            situation.ExpectedWindowEndIndices.Span[0] = 12000;
            return situation;
        }
    }
}
