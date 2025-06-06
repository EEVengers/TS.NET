﻿namespace TS.NET.Tests
{
    internal class FallingEdgeTriggerSituations
    {
        /// <summary>
        /// 100 samples idle, 100 sample wide positive pulse
        /// </summary>
        public static EdgeTriggerSituation SituationA()
        {
            var situation = new EdgeTriggerSituation()
            {
                Parameters = new EdgeTriggerParameters(Level: -20, Hysteresis: 10, EdgeDirection.Falling),
                WindowWidth = 10000,
                WindowTriggerPosition = 0,
                AdditionalHoldoff = 0,

                ChunkSize = 8388608,
                ChunkCount = 1
            };
            situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
            situation.ExpectedWindowEndIndices = new int[1];
            situation.Input.Span.Fill(0);
            situation.Input.Span.Slice(100, 100).Fill(sbyte.MinValue);
            situation.ExpectedWindowEndIndices.Span[0] = 10100;
            return situation;
        }

        /// <summary>
        /// 100 samples idle, 100 sample wide positive pulse, 100 samples idle, 100 sample wide positive pulse. Second pulse should be ignored.
        /// </summary>
        public static EdgeTriggerSituation SituationB()
        {
            var situation = new EdgeTriggerSituation()
            {
                Parameters = new EdgeTriggerParameters(Level: -20, Hysteresis: 10, EdgeDirection.Falling),
                WindowWidth = 10000,
                WindowTriggerPosition = 0,
                AdditionalHoldoff = 0,

                ChunkSize = 8388608,
                ChunkCount = 1
            };
            situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
            situation.ExpectedWindowEndIndices = new int[1];
            situation.Input.Span.Fill(0);
            situation.Input.Span.Slice(100, 100).Fill(sbyte.MinValue);
            situation.Input.Span.Slice(300, 100).Fill(sbyte.MinValue);  // This pulse should be ignored
            situation.ExpectedWindowEndIndices.Span[0] = 10100;
            return situation;
        }

        /// <summary>
        /// 100 samples idle, 100 sample wide positive pulse, 10000 samples idle, 100 sample wide positive pulse.
        /// </summary>
        public static EdgeTriggerSituation SituationC()
        {
            var situation = new EdgeTriggerSituation()
            {
                Parameters = new EdgeTriggerParameters(Level: -20, Hysteresis: 10, EdgeDirection.Falling),
                WindowWidth = 10000,
                WindowTriggerPosition = 0,
                AdditionalHoldoff = 0,

                ChunkSize = 8388608,
                ChunkCount = 1
            };
            situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
            situation.ExpectedWindowEndIndices = new int[2];
            situation.Input.Span.Fill(0);
            situation.Input.Span.Slice(100, 100).Fill(sbyte.MinValue);
            situation.Input.Span.Slice(10200, 100).Fill(sbyte.MinValue);
            situation.ExpectedWindowEndIndices.Span[0] = 10100;
            situation.ExpectedWindowEndIndices.Span[1] = 20200;
            return situation;
        }

        /// <summary>
        /// 100 sample wide positive pulse
        /// </summary>
        public static EdgeTriggerSituation SituationD()
        {
            var situation = new EdgeTriggerSituation()
            {
                Parameters = new EdgeTriggerParameters(Level: -20, Hysteresis: 10, EdgeDirection.Falling),
                WindowWidth = 10000,
                WindowTriggerPosition = 0,
                AdditionalHoldoff = 0,

                ChunkSize = 8388608,
                ChunkCount = 1
            };
            situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
            situation.ExpectedWindowEndIndices = new int[1];
            situation.Input.Span.Fill(0);
            situation.Input.Span.Slice(0, 100).Fill(sbyte.MinValue);
            situation.ExpectedWindowEndIndices.Span[0] = 0;
            return situation;
        }

        /// <summary>
        /// 100 samples idle, 1 sample wide positive pulse
        /// </summary>
        public static EdgeTriggerSituation SituationE()
        {
            var situation = new EdgeTriggerSituation()
            {
                Parameters = new EdgeTriggerParameters(Level: -20, Hysteresis: 10, EdgeDirection.Falling),
                WindowWidth = 10000,
                WindowTriggerPosition = 0,
                AdditionalHoldoff = 0,

                ChunkSize = 8388608,
                ChunkCount = 1
            };
            situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
            situation.ExpectedWindowEndIndices = new int[1];
            situation.Input.Span.Fill(0);
            situation.Input.Span[100] = sbyte.MinValue;
            situation.ExpectedWindowEndIndices.Span[0] = 10100;
            return situation;
        }
    }
}