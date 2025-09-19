using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace TS.NET;

public class ThunderscopeDataConnectionException : Exception
{
    public ThunderscopeDataConnectionException() { }
    public ThunderscopeDataConnectionException(string message) : base(message) { }
}

public class ThunderscopeDataConnection : TcpScpiConnection
{
    public void Open(string ipAddress)
    {
        Open(ipAddress, 5026);
    }

    public void RequestWaveform()
    {
        CheckOpen();
        WriteLine("S");
    }

    public WaveformHeader ReadWaveformHeader(Span<byte> buffer)
    {
        unsafe
        {
            var fixedLengthBuffer = buffer.Slice(0, sizeof(WaveformHeader));
            var count = ReadBytes(fixedLengthBuffer);
            if (count != fixedLengthBuffer.Length)
                throw new ThunderscopeDataConnectionException();
            return MemoryMarshal.Read<WaveformHeader>(fixedLengthBuffer);
        }
    }

    public ChannelHeader ReadChannelHeader(Span<byte> buffer)
    {
        unsafe
        {
            var fixedLengthBuffer = buffer.Slice(0, sizeof(ChannelHeader));
            var count = ReadBytes(fixedLengthBuffer);
            if (count != fixedLengthBuffer.Length)
                throw new ThunderscopeDataConnectionException();
            return MemoryMarshal.Read<ChannelHeader>(fixedLengthBuffer);
        }
    }

    public ReadOnlySpan<T> ReadChannelData<T>(Span<byte> buffer, ChannelHeader channelHeader) where T : unmanaged
    {
        unsafe
        {
            if (typeof(T) == typeof(sbyte) && channelHeader.DataType != 2)
                throw new ThunderscopeDataConnectionException();
            if (typeof(T) == typeof(short) && channelHeader.DataType != 4)
                throw new ThunderscopeDataConnectionException();
            if (typeof(T) != typeof(sbyte) && typeof(T) != typeof(short))
                throw new ThunderscopeDataConnectionException();
            var fixedLengthBuffer = buffer.Slice(0, (int)channelHeader.Depth);
            var count = ReadBytes(fixedLengthBuffer);
            if (count != fixedLengthBuffer.Length)
                throw new ThunderscopeDataConnectionException($"Count: {count}, buffer: {fixedLengthBuffer.Length}");
            return MemoryMarshal.Cast<byte, T>(fixedLengthBuffer);
        }
    }

    private int ReadBytes(Span<byte> buffer)
    {
        CheckOpen();
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            try
            {
                int bytesRead = networkStream!.Read(buffer.Slice(totalRead));
                if (bytesRead == 0)
                    break; // Connection closed by peer

                totalRead += bytesRead;
            }
            catch (IOException ex) when (ex.InnerException is SocketException se && se.SocketErrorCode == SocketError.TimedOut)
            {
                break; // Stop reading on timeout
            }
        }
        return totalRead;
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct WaveformHeader
{
    public byte Version;
    public uint SeqNumber;
    public ushort NumChannels;
    public ulong FsPerSample;
    public long TriggerFs;
    public double HwWaveformsPerSec;

    public override string ToString()
    {
        return $"{nameof(SeqNumber)}: {SeqNumber}, {nameof(NumChannels)}: {NumChannels}, {nameof(FsPerSample)}: {FsPerSample}, {nameof(TriggerFs)}: {TriggerFs}, {nameof(HwWaveformsPerSec)}: {HwWaveformsPerSec}";
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct ChannelHeader
{
    public byte ChannelIndex;
    public ulong Depth;
    public float Scale;
    public float Offset;
    public float TrigPhase;
    public byte Clipping;
    public byte DataType;        // ThunderscopeDataType, I8 = 2, I16 = 4
    public override string ToString()
    {
        return $"{nameof(ChannelIndex)}: {ChannelIndex}, {nameof(Depth)}: {Depth}, {nameof(Scale)}: {Scale}, {nameof(Offset)}: {Offset}, {nameof(TrigPhase)}: {TrigPhase}, {nameof(Clipping)}: {Clipping}";
    }
}