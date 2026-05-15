namespace TS.NET.JTAG;

internal sealed class D2xxMpsseDevice : IDisposable
{
    private readonly D2xxDevice d2xx;
    private readonly Lock ioLock = new();

    private D2xxMpsseDevice(D2xxDevice d2xx)
    {
        this.d2xx = d2xx;
    }

    public static D2xxMpsseDevice OpenViaD2xx(int deviceIndex, uint ioTimeoutMs, uint jtagClockHz, ushort layoutValue, ushort layoutDirection)
    {
        var d2xx = D2xxDevice.Open(deviceIndex);
        var device = new D2xxMpsseDevice(d2xx);
        try
        {
            device.Initialize(jtagClockHz, ioTimeoutMs, layoutValue, layoutDirection);
            return device;
        }
        catch
        {
            device.Dispose();
            throw;
        }
    }

    public byte[] Execute(byte[] command, int expectedPayloadBytes)
    {
        lock (ioLock)
        {
            d2xx.Write(command);

            if (expectedPayloadBytes <= 0)
            {
                return Array.Empty<byte>();
            }

            return d2xx.Read(expectedPayloadBytes);
        }
    }

    public void SetJtagClockHz(uint jtagClockHz)
    {
        var divisor = ComputeClockDivisor(jtagClockHz);
        var command = new byte[]
        {
            0x86,
            (byte)(divisor & 0xFF),
            (byte)(divisor >> 8 & 0xFF),
        };

        Execute(command, 0);
    }

    private void Initialize(uint jtagClockHz, uint ioTimeoutMs, ushort layoutValue, ushort layoutDirection)
    {
        d2xx.Reset();
        d2xx.Purge();
        d2xx.SetTimeouts(ioTimeoutMs);
        d2xx.SetLatencyTimer(2);

        d2xx.SetBitMode(0x00, 0x00);
        d2xx.SetBitMode(0x0B, 0x02);

        var divisor = ComputeClockDivisor(jtagClockHz);
        var lowValue = (byte)(layoutValue & 0xFF);
        var lowDirection = (byte)(layoutDirection & 0xFF);
        var highValue = (byte)(layoutValue >> 8 & 0xFF);
        var highDirection = (byte)(layoutDirection >> 8 & 0xFF);

        var init = new byte[]
        {
            0x8A, // disable /5 clocking for high-speed parts
            0x97, // disable adaptive clocking
            0x8D, // disable 3-phase data clocking
            0x85, // disable loopback
            0x86, (byte)(divisor & 0xFF), (byte)(divisor >> 8 & 0xFF),
            0x80, lowValue, lowDirection,
            0x82, highValue, highDirection,
        };

        Execute(init, 0);
    }

    private static ushort ComputeClockDivisor(uint targetHz)
    {
        if (targetHz == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetHz));
        }

        const uint baseClock = 60_000_000;
        var raw = baseClock / (targetHz * 2) - 1;
        if (raw > ushort.MaxValue)
        {
            raw = ushort.MaxValue;
        }

        return (ushort)raw;
    }

    public void Dispose()
    {
        d2xx.Dispose();
    }
}