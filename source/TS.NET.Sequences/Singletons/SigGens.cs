using System.Buffers.Binary;
using System.Text;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class SigGens
{
    private const int SdgMaxArbitraryLengthBytes = 16_000_000;

    public enum ArbitraryWaveShape
    {
        Sine,
        Square
    }

    private static readonly Lazy<SigGens> lazy = new(() => new SigGens());
    public static SigGens Instance { get { return lazy.Value; } }
    private SigGens() { }

    private TcpScpiConnection? sigGen1;
    private TcpScpiConnection? sigGen2;
    private int[] cachedSigGenChannelIndices = [];

    public void Initialise(string? sigGen1Host, string? sigGen2Host)
    {
        if (string.IsNullOrWhiteSpace(sigGen1Host))
            throw new ArgumentException();
        if (string.IsNullOrWhiteSpace(sigGen2Host))
            throw new ArgumentException();

        sigGen1 = new TcpScpiConnection();
        sigGen1.Open(sigGen1Host, 5025);
        Logger.Instance.Log(LogLevel.Debug, "SCPI connection to signal generator #1 opened.");
        sigGen1.WriteLine("*IDN?");
        var sigGen1Idn = sigGen1.ReadLine();
        Logger.Instance.Log(LogLevel.Debug, $"*IDN: {sigGen1Idn}");
        if (!sigGen1Idn.StartsWith("Siglent Technologies,SDG2", StringComparison.OrdinalIgnoreCase))
            throw new ApplicationException("Incorrect response from *IDN?");

        sigGen1.WriteLine("C2:OUTP OFF"); SdgWaitForCompletion(sigGen1);
        sigGen1.WriteLine("C2:OUTP LOAD, HZ"); SdgWaitForCompletion(sigGen1);
        sigGen1.WriteLine("C2:OUTP PLRT, NOR"); SdgWaitForCompletion(sigGen1);
        sigGen1.WriteLine("C2:BSWV WVTP, DC"); SdgWaitForCompletion(sigGen1);
        sigGen1.WriteLine("C2:BSWV OFST, 0"); SdgWaitForCompletion(sigGen1);
        sigGen1.WriteLine("C2:BSWV AMP, 0"); SdgWaitForCompletion(sigGen1);

        sigGen1.WriteLine("C1:OUTP OFF"); SdgWaitForCompletion(sigGen1);
        sigGen1.WriteLine("C1:OUTP LOAD, HZ"); SdgWaitForCompletion(sigGen1);
        sigGen1.WriteLine("C1:OUTP PLRT, NOR"); SdgWaitForCompletion(sigGen1);
        sigGen1.WriteLine("C1:BSWV WVTP, DC"); SdgWaitForCompletion(sigGen1);
        sigGen1.WriteLine("C1:BSWV OFST, 0"); SdgWaitForCompletion(sigGen1);
        sigGen1.WriteLine("C1:BSWV AMP, 0"); SdgWaitForCompletion(sigGen1);

        sigGen2 = new TcpScpiConnection();
        sigGen2.Open(sigGen2Host, 5025);
        Logger.Instance.Log(LogLevel.Debug, "SCPI connection to signal generator #2 opened.");
        sigGen2.WriteLine("*IDN?");
        var sigGen2Idn = sigGen2.ReadLine();
        Logger.Instance.Log(LogLevel.Debug, $"*IDN: {sigGen2Idn}");
        if (!sigGen2Idn.StartsWith("Siglent Technologies,SDG2", StringComparison.OrdinalIgnoreCase))
            throw new ApplicationException("Incorrect response from *IDN?");

        sigGen2.WriteLine("C2:OUTP OFF"); SdgWaitForCompletion(sigGen2);
        sigGen2.WriteLine("C2:OUTP LOAD, HZ"); SdgWaitForCompletion(sigGen2);
        sigGen2.WriteLine("C2:OUTP PLRT, NOR"); SdgWaitForCompletion(sigGen2);
        sigGen2.WriteLine("C2:BSWV WVTP, DC"); SdgWaitForCompletion(sigGen1);
        sigGen2.WriteLine("C2:BSWV OFST, 0"); SdgWaitForCompletion(sigGen2);
        sigGen2.WriteLine("C2:BSWV AMP, 0"); SdgWaitForCompletion(sigGen2);

        sigGen2.WriteLine("C1:OUTP OFF"); SdgWaitForCompletion(sigGen2);
        sigGen2.WriteLine("C1:OUTP LOAD, HZ"); SdgWaitForCompletion(sigGen2);
        sigGen2.WriteLine("C1:OUTP PLRT, NOR"); SdgWaitForCompletion(sigGen2);
        sigGen2.WriteLine("C1:BSWV WVTP, DC"); SdgWaitForCompletion(sigGen1);
        sigGen2.WriteLine("C1:BSWV OFST, 0"); SdgWaitForCompletion(sigGen2);
        sigGen2.WriteLine("C1:BSWV AMP, 0"); SdgWaitForCompletion(sigGen2);
    }

    public void Close()
    {
        SetSdgChannel([]);
        sigGen1?.Close();
        sigGen2?.Close();
    }

    public void SetSdgChannel(int[] channelIndices)
    {
        EnsureSdgOutputState(sigGen1, "C1", channelIndices.Contains(0));
        EnsureSdgOutputState(sigGen1, "C2", channelIndices.Contains(1));
        EnsureSdgOutputState(sigGen2, "C1", channelIndices.Contains(2));
        EnsureSdgOutputState(sigGen2, "C2", channelIndices.Contains(3));
        cachedSigGenChannelIndices = channelIndices;

        void EnsureSdgOutputState(TcpScpiConnection? sigGen, string sdgChannel, bool shouldBeOn)
        {
            if (sigGen == null)
                return;

            sigGen.WriteLine($"{sdgChannel}:OUTP?");
            var response = sigGen.ReadLine();
            var isOn = ParseSdgOutputEnabled(response);

            if (isOn != shouldBeOn)
            {
                sigGen.WriteLine($"{sdgChannel}:OUTP {(shouldBeOn ? "ON" : "OFF")}");
                SdgWaitForCompletion(sigGen);
            }
        }

        static bool ParseSdgOutputEnabled(string response)
        {
            var tokens = response.Split([',', ':', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            for (var i = 0; i < tokens.Length; i++)
            {
                if (tokens[i].Equals("ON", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (tokens[i].Equals("OFF", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            throw new TestbenchException($"Failed to parse SDG output state from response: '{response}'");
        }
    }

    public void SetSdgLoad(int channelIndex, ThunderscopeTermination termination)
    {
        var load = termination switch
        {
            ThunderscopeTermination.OneMegaohm => "HiZ",
            ThunderscopeTermination.FiftyOhm => "50",
            _ => throw new NotImplementedException()
        };
        GetSdgReference(channelIndex, out var sigGen, out var sdgChannel);
        sigGen.WriteLine($"{sdgChannel}:OUTP LOAD, {load}");
        SdgWaitForCompletion(sigGen);
    }

    public void SetSdgDc(int channelIndex)
    {
        GetSdgReference(channelIndex, out var sigGen, out var sdgChannel);
        sigGen.WriteLine($"{sdgChannel}:BSWV WVTP, DC");
        SdgWaitForCompletion(sigGen);
    }

    public void SetSdgSine(int channelIndex)
    {
        GetSdgReference(channelIndex, out var sigGen, out var sdgChannel);
        sigGen.WriteLine($"{sdgChannel}:BSWV WVTP, SINE");
        SdgWaitForCompletion(sigGen);
    }

    public void SetSdgSquare(int channelIndex)
    {
        GetSdgReference(channelIndex, out var sigGen, out var sdgChannel);
        sigGen.WriteLine($"{sdgChannel}:BSWV WVTP, SQUARE");
        SdgWaitForCompletion(sigGen);
    }

    public void SetSdgNoise(int channelIndex, double stdev, double mean)
    {
        GetSdgReference(channelIndex, out var sigGen, out var sdgChannel);
        sigGen.WriteLine($"{sdgChannel}:BSWV WVTP, NOISE");
        sigGen.WriteLine($"{sdgChannel}:BSWV STDEV, {stdev}");
        sigGen.WriteLine($"{sdgChannel}:BSWV MEAN, {mean}");
        SdgWaitForCompletion(sigGen);
    }

    public int LoadSdgArbitraryBurstList(int channelIndex, uint sampleRate, IReadOnlyList<double> frequenciesHz, int cyclesPerFrequency, string waveName, ArbitraryWaveShape waveShape = ArbitraryWaveShape.Sine)
    {
        if (frequenciesHz is null)
            throw new ArgumentNullException(nameof(frequenciesHz));
        if (frequenciesHz.Count == 0)
            throw new ArgumentException("At least one frequency must be provided.", nameof(frequenciesHz));
        if (cyclesPerFrequency <= 0)
            throw new ArgumentOutOfRangeException(nameof(cyclesPerFrequency), "Cycles per frequency must be > 0.");
        if (string.IsNullOrWhiteSpace(waveName) || waveName.Contains(','))
            throw new ArgumentException("Wave name must be non-empty and cannot contain commas.", nameof(waveName));

        for (var i = 0; i < frequenciesHz.Count; i++)
        {
            var frequencyHz = frequenciesHz[i];
            if (frequencyHz <= 0)
                throw new ArgumentOutOfRangeException(nameof(frequenciesHz), "All frequencies must be > 0 Hz.");
            if (frequencyHz > 10_000_000)
                throw new ArgumentOutOfRangeException(nameof(frequenciesHz), "All frequencies must be <= 10 MHz.");
            if (i > 0 && frequencyHz < frequenciesHz[i - 1])
                throw new ArgumentException("Frequencies must be in ascending order.", nameof(frequenciesHz));
        }

        var samples = new List<short>(4096);
        const int initialDcSampleCount = 750;
        var initialDcLevel = short.MinValue;
        for (var i = 0; i < initialDcSampleCount; i++)
        {
            samples.Add(initialDcLevel);
        }

        foreach (var frequencyHz in frequenciesHz)
        {
            var samplesPerCycle = Math.Max(2, (int)Math.Round(sampleRate / frequencyHz));

            for (var cycle = 0; cycle < cyclesPerFrequency; cycle++)
            {
                for (var i = 0; i < samplesPerCycle; i++)
                {
                    short sample = waveShape switch
                    {
                        ArbitraryWaveShape.Sine => (short)Math.Round(Math.Sin((2.0 * Math.PI * i) / samplesPerCycle) * short.MaxValue),
                        ArbitraryWaveShape.Square => i < (samplesPerCycle / 2) ? short.MaxValue : short.MinValue,
                        _ => throw new NotImplementedException()
                    };
                    samples.Add(sample);

                    var lengthBytes = checked(samples.Count * sizeof(short));
                    if (lengthBytes > SdgMaxArbitraryLengthBytes)
                    {
                        throw new TestbenchException($"Arbitrary waveform would exceed {SdgMaxArbitraryLengthBytes} bytes. " + "Reduce number of frequencies or cyclesPerFrequency.");
                    }
                }
            }
        }

        const int finalDcSampleCount = initialDcSampleCount;
        var finalDcLevel = short.MaxValue;
        for (var i = 0; i < finalDcSampleCount; i++)
        {
            samples.Add(finalDcLevel);

            var lengthBytes = checked(samples.Count * sizeof(short));
            if (lengthBytes > SdgMaxArbitraryLengthBytes)
            {
                throw new TestbenchException($"Arbitrary waveform would exceed {SdgMaxArbitraryLengthBytes} bytes. " + "Reduce number of frequencies, cyclesPerFrequency, or endpoint padding.");
            }
        }

        if (samples.Count == 0)
            throw new TestbenchException("No samples generated for arbitrary waveform.");

        var waveDataBytes = BuildRawWaveData(samples);
        GetSdgReference(channelIndex, out var sigGen, out var sdgChannel);

        var commandPrefix = $"{sdgChannel}:WVDT WVNM,{waveName},OFST,0.0,PHASE,0.0,WAVEDATA,";
        var commandPrefixBytes = Encoding.ASCII.GetBytes(commandPrefix);
        var commandBytes = new byte[commandPrefixBytes.Length + waveDataBytes.Length + 1];
        Buffer.BlockCopy(commandPrefixBytes, 0, commandBytes, 0, commandPrefixBytes.Length);
        Buffer.BlockCopy(waveDataBytes, 0, commandBytes, commandPrefixBytes.Length, waveDataBytes.Length);
        commandBytes[^1] = (byte)'\n';
        sigGen.WriteRaw(commandBytes);
        Thread.Sleep(1000);
        SdgWaitForCompletion(sigGen);

        return samples.Count;

        //sigGen.WriteLine($"{sdgChannel}:BSWV FRQ, {waveformFrequencyHz.ToString("G17", CultureInfo.InvariantCulture)}");
        // SdgWaitForCompletion(sigGen);

        static byte[] BuildRawWaveData(IReadOnlyList<short> samples)
        {
            var payload = new byte[samples.Count * sizeof(short)];
            var offset = 0;

            for (var i = 0; i < samples.Count; i++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(offset), samples[i]);
                offset += sizeof(short);
            }
            //Go through bytes, if any match '\n' then add one to it
            for (var i = 0; i < payload.Length; i++)
            {
                if (payload[i] == (byte)'\n')
                {
                    payload[i]++;
                }
            }

            return payload;
        }
    }

    public void SetSdgArbitraryBurstList(int channelIndex, double amplitudeVpp, uint sampleRate, string waveName = "LOGSWEEP")
    {
        if (string.IsNullOrWhiteSpace(waveName) || waveName.Contains(','))
            throw new ArgumentException("Wave name must be non-empty and cannot contain commas.", nameof(waveName));

        GetSdgReference(channelIndex, out var sigGen, out var sdgChannel);

        sigGen.WriteLine($"{sdgChannel}:BSWV WVTP,ARB,AMP,{amplitudeVpp},OFST,0.0,PHASE,0.0");
        SdgWaitForCompletion(sigGen);

        sigGen.WriteLine($"{sdgChannel}:ARWV NAME,\"{waveName}\"");
        SdgWaitForCompletion(sigGen);

        sigGen.WriteLine($"{sdgChannel}:SRATE MODE,TARB,VALUE,{sampleRate},INTER,LINE");
        SdgWaitForCompletion(sigGen);

        // Configure burst mode: one arbitrary waveform cycle per manual trigger.
        sigGen.WriteLine($"{sdgChannel}:BTWV STATE,ON");
        SdgWaitForCompletion(sigGen);
        sigGen.WriteLine($"{sdgChannel}:BTWV TRSR,MAN");
        SdgWaitForCompletion(sigGen);
        sigGen.WriteLine($"{sdgChannel}:BTWV TIME,1");
        SdgWaitForCompletion(sigGen);
    }

    public static double[] BuildLogSweepFrequencies(double startFrequencyHz, double stopFrequencyHz, int pointsPerDecade)
    {
        if (startFrequencyHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(startFrequencyHz), "Start frequency must be > 0 Hz.");
        if (stopFrequencyHz <= 0)
            throw new ArgumentOutOfRangeException(nameof(stopFrequencyHz), "Stop frequency must be > 0 Hz.");
        if (stopFrequencyHz > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(stopFrequencyHz), "Stop frequency must be <= 1 MHz.");
        if (stopFrequencyHz < startFrequencyHz)
            throw new ArgumentOutOfRangeException(nameof(stopFrequencyHz), "Stop frequency must be >= start frequency.");
        if (pointsPerDecade <= 0)
            throw new ArgumentOutOfRangeException(nameof(pointsPerDecade), "Points per decade must be > 0.");

        if (startFrequencyHz == stopFrequencyHz)
            return [startFrequencyHz];

        var frequencies = new List<double> { startFrequencyHz };
        var ratio = Math.Pow(10.0, 1.0 / pointsPerDecade);
        var nextFrequency = startFrequencyHz;

        while (true)
        {
            nextFrequency *= ratio;
            if (nextFrequency >= stopFrequencyHz)
                break;

            frequencies.Add(nextFrequency);
        }

        if (frequencies[^1] != stopFrequencyHz)
            frequencies.Add(stopFrequencyHz);

        return frequencies.ToArray();
    }

    public void SetSdgParameterFrequency(int channelIndex, uint frequencyHz)
    {
        GetSdgReference(channelIndex, out var sigGen, out var sdgChannel);
        sigGen.WriteLine($"{sdgChannel}:BSWV FRQ, {frequencyHz}");
        SdgWaitForCompletion(sigGen);

        // var valueText = GetSdgParameter(sigGen, sdgChannel, "FRQ,");
        // // Remove trailing 'HZ' if present
        // if (valueText.EndsWith("HZ", StringComparison.OrdinalIgnoreCase))
        //     valueText = valueText[..^2];

        // if (!double.TryParse(valueText, System.Globalization.NumberStyles.Float,
        //                      System.Globalization.CultureInfo.InvariantCulture,
        //                      out var value))
        // {
        //     throw new TestbenchException($"Failed to parse FRQ value from '{valueText}'");
        // }

        // // Compare within tolerance
        // const double tolerance = 1e-4;
        // if (Math.Abs(value - frequencyHz) > tolerance)
        // {
        //     throw new TestbenchException($"Verification failed. Expected {frequencyHz:F4}, but device reports {value:F4}.");
        // }
    }

    public void SetSdgParameterPeriod(int channelIndex, double periodSec)
    {
        GetSdgReference(channelIndex, out var sigGen, out var sdgChannel);
        sigGen.WriteLine($"{sdgChannel}:BSWV PERI, {periodSec}S");
        SdgWaitForCompletion(sigGen);
    }

    public void SetSdgParameterAmplitude(int channelIndex, double amplitudeVpp)
    {
        GetSdgReference(channelIndex, out var sigGen, out var sdgChannel);
        sigGen.WriteLine($"{sdgChannel}:BSWV AMP, {amplitudeVpp}");
        SdgWaitForCompletion(sigGen);
    }

    public void SetSdgParameterOffset(int channelIndex, double voltage)
    {
        GetSdgReference(channelIndex, out var sigGen, out var sdgChannel);
        sigGen.WriteLine($"{sdgChannel}:BSWV OFST, {voltage:F4}");
        SdgWaitForCompletion(sigGen);
    }

    public void TriggerSdgManualBurst(int channelIndex)
    {
        GetSdgReference(channelIndex, out var sigGen, out var sdgChannel);
        sigGen.WriteLine($"{sdgChannel}:BTWV MTRIG");
        SdgWaitForCompletion(sigGen);
    }

    private void SdgWaitForCompletion(TcpScpiConnection sigGen)
    {
        sigGen.WriteLine("*OPC?");
        var response = sigGen.ReadLine();
        if (response.Trim() != "1")
            throw new TestbenchException("Signal generator did not return correct response to *OPC?");
    }

    private void GetSdgReference(int channelIndex, out TcpScpiConnection sigGen, out string sdgChannel)
    {
        if (sigGen1 == null || sigGen2 == null)
            throw new NullReferenceException();

        switch (channelIndex)
        {
            case 0:
                sigGen = sigGen1;
                sdgChannel = "C1";
                return;
            case 1:
                sigGen = sigGen1;
                sdgChannel = "C2";
                return;
            case 2:
                sigGen = sigGen2;
                sdgChannel = "C1";
                return;
            case 3:
                sigGen = sigGen2;
                sdgChannel = "C2";
                return;
            default:
                throw new NotImplementedException();
        }
    }

    //private string GetSdgBasicWaveformParameter(TcpScpiConnection sigGen, string sdgChannel, string key)
    //{
    //    sigGen.WriteLine($"{sdgChannel}:BSWV?");
    //    var response = sigGen.ReadLine();

    //    // Parse value from response
    //    var idx = response.IndexOf(key, StringComparison.OrdinalIgnoreCase);
    //    if (idx < 0)
    //        throw new TestbenchException($"Response does not contain an {key} field: '{response}'");

    //    idx += key.Length;
    //    var endIdx = response.IndexOf(',', idx);
    //    if (endIdx < 0)
    //        endIdx = response.Length;

    //    var valueText = response.Substring(idx, endIdx - idx).Trim();
    //    return valueText;
    //}

    //public void SetSdgBodeSetup(int channelIndex, uint frequency, double amplitudeVpp)
    //{
    //    if (channelIndex != 0)
    //        throw new NotImplementedException();

    //    sigGen1!.WriteLine($"C1:OUTP ON"); SdgWaitForCompletion(sigGen1!);
    //    sigGen1!.WriteLine($"C2:OUTP ON"); SdgWaitForCompletion(sigGen1!);

    //    sigGen2!.WriteLine($"C1:OUTP OFF"); SdgWaitForCompletion(sigGen2!);
    //    sigGen2!.WriteLine($"C2:OUTP OFF"); SdgWaitForCompletion(sigGen2!);

    //    sigGen1!.WriteLine("C1:OUTP LOAD, 50"); SdgWaitForCompletion(sigGen1!);
    //    sigGen1!.WriteLine("C2:OUTP LOAD, 50"); SdgWaitForCompletion(sigGen1!);

    //    SetSdgSine(channelIndex); SdgWaitForCompletion(sigGen1!);
    //    SetSdgParameterFrequency(channelIndex, frequency); SdgWaitForCompletion(sigGen1!);
    //    SetSdgParameterAmplitude(channelIndex, amplitudeVpp); SdgWaitForCompletion(sigGen1!);
    //    SetSdgParameterOffset(channelIndex, 0.0); SdgWaitForCompletion(sigGen1!);

    //    SetSdgSquare(channelIndex + 1); SdgWaitForCompletion(sigGen1!);
    //    SetSdgParameterFrequency(channelIndex + 1, frequency); SdgWaitForCompletion(sigGen1!);
    //    SetSdgParameterAmplitude(channelIndex + 1, 0.8); SdgWaitForCompletion(sigGen1!);
    //    SetSdgParameterOffset(channelIndex + 1, 0.0); SdgWaitForCompletion(sigGen1!);

    //    sigGen1!.WriteLine("COUP FCOUP,ON"); SdgWaitForCompletion(sigGen1!);
    //}

    //public void UpdateSdgBodeFrequency(int channelIndex, uint frequency)
    //{
    //    if (channelIndex != 0)
    //        throw new NotImplementedException();

    //    SetSdgParameterFrequency(channelIndex, frequency); SdgWaitForCompletion(sigGen1!);
    //}
}
