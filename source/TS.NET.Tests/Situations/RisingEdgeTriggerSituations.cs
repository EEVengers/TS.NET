﻿using System;

namespace TS.NET.Tests
{
    public class TriggerSituation
    {
        public sbyte TriggerLevel { get; set; }
        public byte TriggerHysteresis { get; set; }
        public uint WindowWidth { get; set; }
        public uint WindowTriggerPosition { get; set; }
        public uint AdditionalHoldoff { get; set; }

        public uint ChunkSize { get; set; }
        public uint ChunkCount { get; set; }
        public Memory<sbyte> Input { get; set; }
        public Memory<uint>[] ExpectedTriggerIndices { get; set; }
        public Memory<uint>[] ExpectedHoldoffEndIndices { get; set; }
    }

    public class RisingEdgeTriggerSituations
    {
        // Trigger at [0] and [^1]
        //public static TriggerSituation SituationA()
        //{
        //    Memory<byte> inputMemory = new byte[8000000];
        //    var data = inputMemory.Span;

        //    data[0] = 127;
        //    data.Slice(1, 3999999).Fill(255);
        //    data.Slice(4000000, 4000000).Fill(0);
        //    data[7999999] = 127;

        //    // TO DO: fix this
        //    Memory<uint> triggerIndices = new uint[1];
        //    var result = triggerIndices.Span;
        //    //result[0] = 0x01;
        //    //result[^1] = 0x8000000000000000;

        //    return new() { TriggerLevel = 127, ArmLevel = 117, HoldoffSamples = 1000, Input = inputMemory, ExpectedTriggerIndices = triggerIndices };
        //}

        //4 sample wide 1 hz pulse at sample 0
        public static TriggerSituation SituationB()
        {
            const int chunkCount = 120;
            TriggerSituation situation = new TriggerSituation()
            {
                TriggerLevel = 127,
                TriggerHysteresis = 10,
                WindowWidth = 50 * 1000000,
                WindowTriggerPosition = 0,
                AdditionalHoldoff = 0,

                ChunkSize = 8388608,
                ChunkCount = chunkCount,
                Input = new sbyte[8388608 * chunkCount],
                
                ExpectedTriggerIndices = new Memory<uint>[chunkCount],
                ExpectedHoldoffEndIndices = new Memory<uint>[chunkCount],
            };

            situation.Input.Span.Fill(sbyte.MinValue);
            situation.Input.Span[0] = sbyte.MaxValue;
            situation.Input.Span[1] = sbyte.MaxValue;
            situation.Input.Span[2] = sbyte.MaxValue;
            situation.Input.Span[3] = sbyte.MaxValue;

            situation.ExpectedTriggerIndices[0] = new uint[1];
            situation.ExpectedTriggerIndices[0].Span[0] = 0;
            //var quotient = situation.HoldoffSamples / situation.ChunkSize;
            //var remainder = situation.HoldoffSamples % situation.ChunkSize;
            //situation.ExpectedHoldoffEndIndices[quotient] = new uint[1];
            //situation.ExpectedHoldoffEndIndices[quotient].Span[0] = remainder;

            return situation;
        }

        //4 sample wide 51hz pulse repeated 3 times
        public static TriggerSituation SituationC()
        {
            const int chunkCount = 120;
            TriggerSituation situation = new TriggerSituation()
            {
                TriggerLevel = 127,
                TriggerHysteresis = 10,
                WindowWidth = 5 * 1000000,
                WindowTriggerPosition = 0,
                AdditionalHoldoff = 0,

                ChunkSize = 8388608,
                ChunkCount = chunkCount,
                Input = new sbyte[8388608 * chunkCount],

                ExpectedTriggerIndices = new Memory<uint>[chunkCount],
                ExpectedHoldoffEndIndices = new Memory<uint>[chunkCount],
            };

            // Every 4901960, a pulse
            situation.Input.Span.Fill(sbyte.MinValue);
            for(int i = 0; i < situation.Input.Length; i+= 4901960)
            {
                situation.Input.Span[i] = sbyte.MaxValue;
                situation.Input.Span[i+1] = sbyte.MaxValue;
                situation.Input.Span[i+2] = sbyte.MaxValue;
                situation.Input.Span[i+3] = sbyte.MaxValue;
            }    

            situation.ExpectedTriggerIndices[0] = new uint[1];
            situation.ExpectedTriggerIndices[0].Span[0] = 0;
            //var quotient = situation.HoldoffSamples / situation.ChunkSize;
            //var remainder = situation.HoldoffSamples % situation.ChunkSize;
            //situation.ExpectedHoldoffEndIndices[quotient] = new uint[1];
            //situation.ExpectedHoldoffEndIndices[quotient].Span[0] = remainder;

            return situation;
        }
    }
}