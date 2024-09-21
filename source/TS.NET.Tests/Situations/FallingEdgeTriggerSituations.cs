using System;

namespace TS.NET.Tests
{
    public class FallingEdgeTriggerSituations
    {
        /// <summary>
        /// 100 samples idle, 100 sample wide positive pulse
        /// </summary>
        public static EdgeTriggerSituation SituationA()
        {
            var situation = new EdgeTriggerSituation()
            {
                TriggerLevel = -50,
                TriggerHysteresis = 10,
                WindowWidth = 10000,
                WindowTriggerPosition = 0,
                AdditionalHoldoff = 0,

                ChunkSize = 8388608,
                ChunkCount = 1
            };
            situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
            situation.ExpectedWindowEndIndices = new uint[1];

            situation.Input.Span.Fill(0);
            for (int i = 100; i <= 200; i++)
            {
                situation.Input.Span[i] = sbyte.MinValue;
            }

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
                TriggerLevel = -50,
                TriggerHysteresis = 10,
                WindowWidth = 10000,
                WindowTriggerPosition = 0,
                AdditionalHoldoff = 0,

                ChunkSize = 8388608,
                ChunkCount = 1
            };
            situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
            situation.ExpectedWindowEndIndices = new uint[1];

            situation.Input.Span.Fill(0);
            for (int i = 100; i <= 200; i++)
            {
                situation.Input.Span[i] = sbyte.MinValue;
            }
            // This pulse should be ignored
            for (int i = 300; i <= 400; i++)
            {
                situation.Input.Span[i] = sbyte.MinValue;
            }

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
                TriggerLevel = -50,
                TriggerHysteresis = 10,
                WindowWidth = 10000,
                WindowTriggerPosition = 0,
                AdditionalHoldoff = 0,

                ChunkSize = 8388608,
                ChunkCount = 1
            };
            situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
            situation.ExpectedWindowEndIndices = new uint[2];

            situation.Input.Span.Fill(0);
            for (int i = 100; i <= 200; i++)
            {
                situation.Input.Span[i] = sbyte.MinValue;
            }
            for (int i = 10200; i <= 10300; i++)
            {
                situation.Input.Span[i] = sbyte.MinValue;
            }

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
                TriggerLevel = -50,
                TriggerHysteresis = 10,
                WindowWidth = 10000,
                WindowTriggerPosition = 0,
                AdditionalHoldoff = 0,

                ChunkSize = 8388608,
                ChunkCount = 1
            };
            situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
            situation.ExpectedWindowEndIndices = new uint[1];

            situation.Input.Span.Fill(0);
            for (int i = 0; i <= 100; i++)
            {
                situation.Input.Span[i] = sbyte.MinValue;
            }

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
                TriggerLevel = -50,
                TriggerHysteresis = 10,
                WindowWidth = 10000,
                WindowTriggerPosition = 0,
                AdditionalHoldoff = 0,

                ChunkSize = 8388608,
                ChunkCount = 1
            };
            situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
            situation.ExpectedWindowEndIndices = new uint[1];

            situation.Input.Span.Fill(0);
            situation.Input.Span[100] = sbyte.MinValue;

            situation.ExpectedWindowEndIndices.Span[0] = 10100;

            return situation;
        }

        ////4 sample wide 1 hz pulse at sample 0
        //public static EdgeTriggerSituation SituationC()
        //{
        //    const int chunkCount = 120;
        //    EdgeTriggerSituation situation = new EdgeTriggerSituation()
        //    {
        //        TriggerLevel = 127,
        //        TriggerHysteresis = 10,
        //        WindowWidth = -50 * 1000000,
        //        WindowTriggerPosition = 0,
        //        AdditionalHoldoff = 0,

        //        ChunkSize = 8388608,
        //        ChunkCount = chunkCount,
        //    };
        //    situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
        //    situation.ExpectedWindowEndIndices = new uint[1];

        //    situation.Input.Span.Fill(sbyte.MinValue);
        //    situation.Input.Span[0] = sbyte.MinValue;
        //    situation.Input.Span[1] = sbyte.MinValue;
        //    situation.Input.Span[2] = sbyte.MinValue;
        //    situation.Input.Span[3] = sbyte.MinValue;

        //    //situation.ExpectedWindowEndIndices[0] = new uint[1];
        //    //var quotient = situation.HoldoffSamples / situation.ChunkSize;
        //    //var remainder = situation.HoldoffSamples % situation.ChunkSize;
        //    //situation.ExpectedHoldoffEndIndices[quotient] = new uint[1];
        //    //situation.ExpectedHoldoffEndIndices[quotient].Span[0] = remainder;

        //    return situation;
        //}

        ////4 sample wide 51hz pulse repeated 3 times
        //public static EdgeTriggerSituation SituationD()
        //{
        //    const int chunkCount = 120;
        //    EdgeTriggerSituation situation = new EdgeTriggerSituation()
        //    {
        //        TriggerLevel = 127,
        //        TriggerHysteresis = 10,
        //        WindowWidth = 5 * 1000000,
        //        WindowTriggerPosition = 0,
        //        AdditionalHoldoff = 0,

        //        ChunkSize = 8388608,
        //        ChunkCount = chunkCount,
        //    };
        //    situation.Input = new sbyte[situation.ChunkSize * situation.ChunkCount];
        //    situation.ExpectedWindowEndIndices = new uint[1];

        //    // Every 4901960, a pulse
        //    situation.Input.Span.Fill(sbyte.MinValue);
        //    for(int i = 0; i < situation.Input.Length; i+= 4901960)
        //    {
        //        situation.Input.Span[i] = sbyte.MinValue;
        //        situation.Input.Span[i+1] = sbyte.MinValue;
        //        situation.Input.Span[i+2] = sbyte.MinValue;
        //        situation.Input.Span[i+3] = sbyte.MinValue;
        //    }    

        //    //situation.ExpectedWindowEndIndices[0] = new uint[1];
        //    //var quotient = situation.HoldoffSamples / situation.ChunkSize;
        //    //var remainder = situation.HoldoffSamples % situation.ChunkSize;
        //    //situation.ExpectedHoldoffEndIndices[quotient] = new uint[1];
        //    //situation.ExpectedHoldoffEndIndices[quotient].Span[0] = remainder;

        //    return situation;
        //}
    }
}