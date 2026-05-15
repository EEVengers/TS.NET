namespace TS.NET.JTAG;

internal static class D2xxDiscovery
{
    internal sealed record D2xxDeviceInfo(int DeviceIndex, uint DeviceID, string Serial, string Description);

    public static unsafe IReadOnlyList<D2xxDeviceInfo> FindDevices()
    {
        D2xx.Interop.EnsureAvailable();
        uint count = 0;
        var status = D2xx.Interop.CreateDeviceInfoList(ref count);

        ThrowIfFailed(status, "CreateDeviceInfoList failed");
        var matches = new List<D2xxDeviceInfo>();

        for (uint i = 0; i < count; i++)
        {
            uint flags = 0;
            uint type = 0;
            uint id = 0;
            uint locationId = 0;
            nint handle = nint.Zero;

            Span<byte> serial = new byte[16];
            Span<byte> description = new byte[64];

            fixed (byte* serialP = serial)
            fixed (byte* descriptionP = description)
            {
                status = D2xx.Interop.GetDeviceInfoDetail(i, ref flags, ref type, ref id, ref locationId, serialP, descriptionP, ref handle);
            }

            if (status != D2xx.Interop.FtStatus.Ok)
            {
                continue;
            }

            string serialText = D2xx.Interop.ReadNullTerminatedUtf8(serial);
            string descriptionText = D2xx.Interop.ReadNullTerminatedUtf8(description);
            matches.Add(new D2xxDeviceInfo((int)i, id, serialText, descriptionText));
        }

        return matches;
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
