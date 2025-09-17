using System.Buffers;
using TS.NET.Sequencer;

namespace TS.NET.Calibration;

public class Instruments
{
    private static readonly Lazy<Instruments> lazy = new(() => new Instruments());
    public static Instruments Instance { get { return lazy.Value; } }
    private Instruments() { }

    private ThunderscopeScpiConnection? thunderScope;
    private ThunderscopeDataConnection? thunderScopeData;
    private TcpScpiConnection? sigGen1;
    private TcpScpiConnection? sigGen2;

    public void Initialise(bool initSigGens)
    {
        // ThunderScope
        thunderScope = new ThunderscopeScpiConnection();
        thunderScope.Open(Variables.Instance.ThunderScopeIp);
        Logger.Instance.Log(LogLevel.Debug, "SCPI connection to ThunderScope opened.");
        thunderScope.WriteLine("*IDN?");
        var thunderScopeIdn = thunderScope.ReadLine();
        Logger.Instance.Log(LogLevel.Debug, $"*IDN: {thunderScopeIdn}");
        if (!thunderScopeIdn.StartsWith("EEVengers,ThunderScope", StringComparison.OrdinalIgnoreCase))
            throw new ApplicationException("Incorrect response from SCPI instrument (Scope).");

        thunderScopeData = new ThunderscopeDataConnection();
        thunderScopeData.Open(Variables.Instance.ThunderScopeIp);
        Logger.Instance.Log(LogLevel.Debug, "Data connection to ThunderScope opened.");

        thunderScope.WriteLine("STOP");
        thunderScope.WriteLine("NORMAL");
        thunderScope.WriteLine("TRIG:SOURCE NONE");
        thunderScope.WriteLine("TRIG:TYPE EDGE");
        thunderScope.WriteLine("TRIG:DELAY 500000000");   // Halfway through capture
        thunderScope.WriteLine("TRIG:HOLD 0");
        thunderScope.WriteLine("TRIG:INTER 1");
        thunderScope.WriteLine("DEPTH 1000000");

        thunderScope.WriteLine("CAL:MANUAL CHAN1 DC 50 0 2786 167 0 1 20M");
        thunderScope.WriteLine("CAL:MANUAL CHAN2 DC 50 0 2786 167 0 1 20M");
        thunderScope.WriteLine("CAL:MANUAL CHAN3 DC 50 0 2786 167 0 1 20M");
        thunderScope.WriteLine("CAL:MANUAL CHAN4 DC 50 0 2786 167 0 1 20M");

        thunderScope.WriteLine("RUN");

        // Sig gen 1 (SDG2042X)
        if (Variables.Instance.SigGen1Ip != null && initSigGens)
        {
            sigGen1 = new TcpScpiConnection();
            sigGen1.Open(Variables.Instance.SigGen1Ip, 5025);
            Logger.Instance.Log(LogLevel.Debug, "SCPI connection to SDG2042X #1 opened.");
            sigGen1.WriteLine("*IDN?");
            var sigGen1Idn = sigGen1.ReadLine();
            Logger.Instance.Log(LogLevel.Debug, $"*IDN: {sigGen1Idn}");
            if (!sigGen1Idn.StartsWith("Siglent Technologies,SDG2042X", StringComparison.OrdinalIgnoreCase))
                throw new ApplicationException("Incorrect response from *IDN?");

            sigGen1.WriteLine("C1:OUTP OFF"); Thread.Sleep(50);
            sigGen1.WriteLine("C1:OUTP LOAD, HZ"); Thread.Sleep(50);
            sigGen1.WriteLine("C1:OUTP PLRT, NOR"); Thread.Sleep(50);
            sigGen1.WriteLine("C1:BSWV WVTP, DC"); Thread.Sleep(50);
            sigGen1.WriteLine("C1:BSWV OFST, 0"); Thread.Sleep(50);
            sigGen1.WriteLine("C1:BSWV AMP, 0"); Thread.Sleep(50);

            sigGen1.WriteLine("C2:OUTP OFF"); Thread.Sleep(50);
            sigGen1.WriteLine("C2:OUTP LOAD, HZ"); Thread.Sleep(50);
            sigGen1.WriteLine("C2:OUTP PLRT, NOR"); Thread.Sleep(50);
            sigGen1.WriteLine("C2:BSWV WVTP, DC"); Thread.Sleep(50);
            sigGen1.WriteLine("C2:BSWV OFST, 0"); Thread.Sleep(50);
            sigGen1.WriteLine("C2:BSWV AMP, 0"); Thread.Sleep(50);
        }

        // Sig gen 2 (SDG2042X)
        if (Variables.Instance.SigGen2Ip != null && initSigGens)
        {
            sigGen2 = new TcpScpiConnection();
            sigGen2.Open(Variables.Instance.SigGen2Ip, 5025);
            Logger.Instance.Log(LogLevel.Debug, "SCPI connection to SDG2042X #2 opened.");
            sigGen2.WriteLine("*IDN?");
            var sigGen2Idn = sigGen2.ReadLine();
            Logger.Instance.Log(LogLevel.Debug, $"*IDN: {sigGen2Idn}");
            if (!sigGen2Idn.StartsWith("Siglent Technologies,SDG2042X", StringComparison.OrdinalIgnoreCase))
                throw new ApplicationException("Incorrect response from *IDN?");

            sigGen2.WriteLine("C1:OUTP OFF"); Thread.Sleep(50);
            sigGen2.WriteLine("C1:OUTP LOAD, HZ"); Thread.Sleep(50);
            sigGen2.WriteLine("C1:OUTP PLRT, NOR"); Thread.Sleep(50);
            sigGen2.WriteLine("C1:BSWV WVTP, DC"); Thread.Sleep(50);
            sigGen2.WriteLine("C1:BSWV OFST, 0"); Thread.Sleep(50);
            sigGen2.WriteLine("C1:BSWV AMP, 0"); Thread.Sleep(50);

            sigGen2.WriteLine("C2:OUTP OFF"); Thread.Sleep(50);
            sigGen2.WriteLine("C2:OUTP LOAD, HZ"); Thread.Sleep(50);
            sigGen2.WriteLine("C2:OUTP PLRT, NOR"); Thread.Sleep(50);
            sigGen2.WriteLine("C2:BSWV WVTP, DC"); Thread.Sleep(50);
            sigGen2.WriteLine("C2:BSWV OFST, 0"); Thread.Sleep(50);
            sigGen2.WriteLine("C2:BSWV AMP, 0"); Thread.Sleep(50);
        }
    }

    public void Close()
    {
        thunderScope?.Close();
        thunderScopeData?.Close();
        sigGen1?.Close();
        sigGen2?.Close();
    }

    public void SetThunderscopeChannel(int[] enabledChannelIndices)
    {
        thunderScope?.WriteLine($"CHAN1:{(enabledChannelIndices.Contains(0) ? "ON" : "OFF")}");
        thunderScope?.WriteLine($"CHAN2:{(enabledChannelIndices.Contains(1) ? "ON" : "OFF")}");
        thunderScope?.WriteLine($"CHAN3:{(enabledChannelIndices.Contains(2) ? "ON" : "OFF")}");
        thunderScope?.WriteLine($"CHAN4:{(enabledChannelIndices.Contains(3) ? "ON" : "OFF")}");
        // Set a default rate so that sequences get a consistent behaviour
        switch (enabledChannelIndices.Length)
        {
            case 1:
                SetThunderscopeRate(1_000_000_000);
                break;
            case 2:
                SetThunderscopeRate(500_000_000);
                break;
            case 3:
                SetThunderscopeRate(250_000_000);
                break;
            case 4:
                SetThunderscopeRate(250_000_000);
                break;
            default:
                throw new NotImplementedException();
        }
    }

    public void SetThunderscopeRate(uint rateHz)
    {
        thunderScope?.WriteLine($"ACQ:RATE {rateHz}");
        Thread.Sleep(Variables.Instance.FrontEndSettlingTimeMs);
    }

    public void SetThunderscopeCalManual50R(int channelIndex, ushort dac, byte dpot, PgaPreampGain pgaPreampGain, byte pgaLadderAttenuation)
    {
        thunderScope?.WriteLine($"CAL:MANUAL CHAN{channelIndex + 1} DC 50 0 {dac} {dpot} {pgaLadderAttenuation} {(pgaPreampGain == PgaPreampGain.High ? "1" : "0")} 20M");
        Thread.Sleep(Variables.Instance.FrontEndSettlingTimeMs);
    }

    public void SetThunderscopeCalManual1M(int channelIndex, ushort dac, byte dpot, PgaPreampGain pgaPreampGain, byte pgaLadderAttenuation)
    {
        thunderScope?.WriteLine($"CAL:MANUAL CHAN{channelIndex + 1} DC 1M 0 {dac} {dpot} {pgaLadderAttenuation} {(pgaPreampGain == PgaPreampGain.High ? "1" : "0")} 20M");
        Thread.Sleep(Variables.Instance.FrontEndSettlingTimeMs);
    }

    public void SetThunderscopeCalManual1M(int channelIndex, bool attenuator, ushort dac, byte dpot, PgaPreampGain pgaPreampGain, byte pgaLadderAttenuation)
    {
        thunderScope?.WriteLine($"CAL:MANUAL CHAN{channelIndex + 1} DC 1M {(attenuator ? "1" : "0")} {dac} {dpot} {pgaLadderAttenuation} {(pgaPreampGain == PgaPreampGain.High ? "1" : "0")} 20M");
        Thread.Sleep(Variables.Instance.FrontEndSettlingTimeMs);
    }

    public double GetThunderscopeAverage(int channelIndex)
    {
        thunderScope!.WriteLine("FORCE");
        var tsDataBuffer = ArrayPool<byte>.Shared.Rent(2_000_000);
        thunderScopeData!.RequestWaveform();
        var waveformHeader = thunderScopeData!.ReadWaveformHeader(tsDataBuffer);

        bool channelFound = false;
        double average = 0;
        for(int i = 0; i < waveformHeader.NumChannels; i++)
        {
            var channelHeader = thunderScopeData.ReadChannelHeader(tsDataBuffer);
            var channelData = thunderScopeData.ReadChannelData<sbyte>(tsDataBuffer, channelHeader);
            if (channelHeader.ChannelIndex != channelIndex)
                continue;
            channelFound = true;
            int sum = 0;
            int min = int.MaxValue;
            int max = int.MinValue;
            foreach (var point in channelData)
            {
                sum += point;
                if (point < min) min = point;
                if (point > max) max = point;
            }
            average = (double)sum / channelData.Length;
            ArrayPool<byte>.Shared.Return(tsDataBuffer);
        }

        if (!channelFound)
            throw new CalibrationException("Channel was not in waveform data");

        return average;
    }

    public void EnableSdgDc(int channelIndex)
    {
        switch (channelIndex)
        {
            case -1:
                sigGen1.WriteLine($"C1:OUTP OFF"); Thread.Sleep(200);
                sigGen1.WriteLine($"C2:OUTP OFF"); Thread.Sleep(200);
                sigGen2.WriteLine($"C1:OUTP OFF"); Thread.Sleep(200);
                sigGen2.WriteLine($"C2:OUTP OFF"); Thread.Sleep(200);
                break;
            case 0:
                sigGen1.WriteLine($"C2:OUTP OFF"); Thread.Sleep(200);
                sigGen2.WriteLine($"C1:OUTP OFF"); Thread.Sleep(200);
                sigGen2.WriteLine($"C2:OUTP OFF"); Thread.Sleep(200);

                sigGen1.WriteLine($"C1:OUTP ON"); Thread.Sleep(200);
                break;
            case 1:
                sigGen1.WriteLine($"C1:OUTP OFF"); Thread.Sleep(200);
                sigGen2.WriteLine($"C1:OUTP OFF"); Thread.Sleep(200);
                sigGen2.WriteLine($"C2:OUTP OFF"); Thread.Sleep(200);

                sigGen1.WriteLine($"C2:OUTP ON"); Thread.Sleep(200);
                break;
            case 2:
                sigGen1.WriteLine($"C1:OUTP OFF"); Thread.Sleep(200);
                sigGen1.WriteLine($"C2:OUTP OFF"); Thread.Sleep(200);
                sigGen2.WriteLine($"C2:OUTP OFF"); Thread.Sleep(200);

                sigGen2.WriteLine($"C1:OUTP ON"); Thread.Sleep(200);
                break;
            case 3:
                sigGen1.WriteLine($"C1:OUTP OFF"); Thread.Sleep(200);
                sigGen1.WriteLine($"C2:OUTP OFF"); Thread.Sleep(200);
                sigGen2.WriteLine($"C1:OUTP OFF"); Thread.Sleep(200);

                sigGen2.WriteLine($"C2:OUTP ON"); Thread.Sleep(200);
                break;
        }
    }

    public void SetSdgDcOffset(int channelIndex, double voltage)
    {
        switch (channelIndex)
        {
            case 0:
                sigGen1.WriteLine($"C1:BSWV OFST, {voltage:F4}"); Thread.Sleep(50);
                break;
            case 1:
                sigGen1.WriteLine($"C2:BSWV OFST, {voltage:F4}"); Thread.Sleep(50);
                break;
            case 2:
                sigGen2.WriteLine($"C1:BSWV OFST, {voltage:F4}"); Thread.Sleep(50);
                break;
            case 3:
                sigGen2.WriteLine($"C2:BSWV OFST, {voltage:F4}"); Thread.Sleep(50);
                break;
        }
    }
}
