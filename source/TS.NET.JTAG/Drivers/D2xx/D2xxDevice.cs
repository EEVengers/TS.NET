namespace TS.NET.JTAG;

internal class D2xxDevice : IDisposable
{
    private nint handle;

    private D2xxDevice(nint handle)
    {
        this.handle = handle;
    }

    public static D2xxDevice Open(int deviceIndex)
    {
        D2xx.Interop.EnsureAvailable();
        var status = D2xx.Interop.Open(deviceIndex, out var handle);
        ThrowIfFailed(status, $"Open failed for device index {deviceIndex}.");
        return new D2xxDevice(handle);
    }

    public void Reset()
    {
        ThrowIfDisposed();
        ThrowIfFailed(D2xx.Interop.ResetDevice(handle), "ResetDevice failed.");
    }

    public void Purge()
    {
        ThrowIfDisposed();
        ThrowIfFailed(D2xx.Interop.Purge(handle, D2xx.Interop.PurgeMask.Rx | D2xx.Interop.PurgeMask.Tx), "Purge failed.");
    }

    public void SetTimeouts(uint timeoutMs)
    {
        ThrowIfDisposed();
        ThrowIfFailed(D2xx.Interop.SetTimeouts(handle, timeoutMs, timeoutMs), "SetTimeouts failed.");
    }

    public void SetLatencyTimer(byte latency)
    {
        ThrowIfDisposed();
        ThrowIfFailed(D2xx.Interop.SetLatencyTimer(handle, latency), "SetLatencyTimer failed.");
    }

    public void SetBitMode(byte mask, byte mode)
    {
        ThrowIfDisposed();
        ThrowIfFailed(D2xx.Interop.SetBitMode(handle, mask, mode), "SetBitMode failed.");
    }

    public void Write(byte[] data)
    {
        ThrowIfDisposed();

        uint written = 0;
        var status = D2xx.Interop.Write(handle, data, (uint)data.Length, ref written);
        ThrowIfFailed(status, "Write failed.");
        if (written != data.Length)
        {
            throw new IOException($"Write wrote {written} bytes of {data.Length}.");
        }
    }

    public byte[] Read(int byteCount)
    {
        ThrowIfDisposed();

        if (byteCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCount));
        }

        if (byteCount == 0)
        {
            return Array.Empty<byte>();
        }

        var buffer = new byte[byteCount];
        var chunk = new byte[Math.Min(byteCount, 4096)];
        var offset = 0;

        while (offset < byteCount)
        {
            var remaining = byteCount - offset;
            var bytesToRead = Math.Min(remaining, chunk.Length);
            uint read = 0;
            var status = D2xx.Interop.Read(handle, chunk, (uint)bytesToRead, ref read);
            ThrowIfFailed(status, "Read failed.");
            if (read == 0)
            {
                throw new TimeoutException("Read timed out before all bytes were received.");
            }

            Buffer.BlockCopy(chunk, 0, buffer, offset, (int)read);
            offset += (int)read;
        }

        return buffer;
    }

    public void Dispose()
    {
        if (handle == nint.Zero)
        {
            return;
        }

        D2xx.Interop.Close(handle);
        handle = nint.Zero;
    }

    private void ThrowIfDisposed()
    {
        if (handle == nint.Zero)
        {
            throw new ObjectDisposedException(nameof(D2xxDevice));
        }
    }

    private static void ThrowIfFailed(D2xx.Interop.FtStatus status, string message)
    {
        if (status == D2xx.Interop.FtStatus.Ok)
        {
            return;
        }

        throw new D2xxException($"{message}, status: {status}");
    }
}
