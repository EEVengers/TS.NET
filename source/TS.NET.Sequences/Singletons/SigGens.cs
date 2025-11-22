using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class SigGens
{
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
        if (!channelIndices.SequenceEqual(cachedSigGenChannelIndices))
        {
            sigGen1?.WriteLine($"C1:OUTP {(channelIndices.Contains(0) ? "ON" : "OFF")}"); SdgWaitForCompletion(sigGen1!);
            sigGen1?.WriteLine($"C2:OUTP {(channelIndices.Contains(1) ? "ON" : "OFF")}"); SdgWaitForCompletion(sigGen1!);
            sigGen2?.WriteLine($"C1:OUTP {(channelIndices.Contains(2) ? "ON" : "OFF")}"); SdgWaitForCompletion(sigGen2!);
            sigGen2?.WriteLine($"C2:OUTP {(channelIndices.Contains(3) ? "ON" : "OFF")}"); SdgWaitForCompletion(sigGen2!);
            cachedSigGenChannelIndices = channelIndices;
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
