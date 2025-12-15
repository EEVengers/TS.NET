using HidSharp;

namespace TS.NET.Sequences
{
    /// <summary>
    /// Leo Bodnar LBE-142x GPS-locked clock source (LBE-1420 single, LBE-1421 dual)
    /// </summary>
    public sealed class LBE142x : IDisposable
    {
        private const int DefaultVendorId = 0x1dd2;
        private const int ProductIdLbe1420 = 0x2443;
        private const int ProductIdLbe1421 = 0x2444;

        private const int ReportLength = 64;
        private const byte ReportId = 0x4B;

        private const byte CmdEnableOutputs = 0x01;
        private const byte CmdBlinkLeds = 0x02;
        private const byte CmdSetF1Temp = 0x05;
        private const byte CmdSetF1 = 0x06;
        private const byte CmdSetF2Temp = 0x09;
        private const byte CmdSetF2 = 0x0A;
        private const byte CmdSetPllMode = 0x0B;
        private const byte CmdSetPps = 0x0C;
        private const byte CmdSetPwr1 = 0x0D;
        private const byte CmdSetPwr2 = 0x0E;

        private const byte StatusGpsLockBit = 1 << 0;
        private const byte StatusPllLockBit = 1 << 1;
        private const byte StatusAntOkBit = 1 << 2;
        private const byte StatusLed1Bit = 1 << 3;
        private const byte StatusLed2Bit = 1 << 4;
        private const byte StatusOut1EnBit = 1 << 5;
        private const byte StatusOut2EnBit = 1 << 6;
        private const byte StatusPpsEnBit = 1 << 7;
        
        public const uint MaxFrequencyHz = 1400000000u;

        private readonly Lock sync = new();
        private HidDevice? device;
        private HidStream? stream;
        private bool disposed;

        public LbeModel Model { get; private set; }

        public bool IsConnected => stream != null && device != null;

        public void Connect()
        {
            ThrowIfDisposed();

            lock (sync)
            {
                if (IsConnected)
                {
                    return;
                }

                device = DeviceList.Local.GetHidDevices(DefaultVendorId).FirstOrDefault(d => d.ProductID == ProductIdLbe1420 || d.ProductID == ProductIdLbe1421);

                if (device == null)
                {
                    throw new InvalidOperationException($"LBE-142x device not found.");
                }

                if (!device.TryOpen(out var openedStream))
                {
                    throw new InvalidOperationException("Failed to open HID stream for LBE-1421.");
                }

                stream = openedStream;
                stream.ReadTimeout = 1000;
                stream.WriteTimeout = 1000;

                Model = device.ProductID == ProductIdLbe1420 ? LbeModel.Lbe1420 : LbeModel.Lbe1421;
            }
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Connect();
            }, cancellationToken);
        }

        public void Disconnect()
        {
            lock (sync)
            {
                stream?.Dispose();
                stream = null;
                device = null;
            }
        }

        /// <summary>
        /// Sets and saves (to flash) the output frequency for OUT1/OUT2.
        /// For LBE-1420, only OUT1 is valid.
        /// </summary>
        public void SetFrequency(int output, uint frequencyHz)
        {
            ThrowIfDisposed();
            EnsureConnected();

            if (output != 1 && output != 2)
            {
                throw new ArgumentOutOfRangeException(nameof(output), "Output must be 1 or 2.");
            }

            if (Model == LbeModel.Lbe1420 && output != 1)
            {
                throw new InvalidOperationException("LBE-1420 only supports output 1.");
            }

            if (frequencyHz < 1 || frequencyHz > MaxFrequencyHz)
            {
                throw new ArgumentOutOfRangeException(nameof(frequencyHz),
                    $"Frequency must be in range 1-{MaxFrequencyHz} Hz.");
            }

            var buf = new byte[ReportLength];
            buf[0] = ReportId;

            if (Model == LbeModel.Lbe1420)
            {
                // 1420 single output layout
                buf[1] = CmdSetF1;
                buf[2] = (byte)((frequencyHz >> 0) & 0xff);
                buf[3] = (byte)((frequencyHz >> 8) & 0xff);
                buf[4] = (byte)((frequencyHz >> 16) & 0xff);
                buf[5] = (byte)((frequencyHz >> 24) & 0xff);
            }
            else
            {
                // 1421 dual output layout
                if (output == 1)
                {
                    buf[1] = CmdSetF1;
                }
                else
                {
                    buf[1] = CmdSetF2;
                }
                buf[6] = (byte)((frequencyHz >> 0) & 0xff);
                buf[7] = (byte)((frequencyHz >> 8) & 0xff);
                buf[8] = (byte)((frequencyHz >> 16) & 0xff);
                buf[9] = (byte)((frequencyHz >> 24) & 0xff);
            }

            SendFeatureReport(buf);
        }

        /// <summary>
        /// Sets a temporary output frequency (not saved to flash).
        /// For LBE-1420, only OUT1 is valid.
        /// </summary>
        public void SetFrequencyTemporary(int output, uint frequencyHz)
        {
            ThrowIfDisposed();
            EnsureConnected();

            if (output != 1 && output != 2)
            {
                throw new ArgumentOutOfRangeException(nameof(output), "Output must be 1 or 2.");
            }

            if (Model == LbeModel.Lbe1420 && output != 1)
            {
                throw new InvalidOperationException("LBE-1420 only supports output 1.");
            }

            if (frequencyHz < 1 || frequencyHz > MaxFrequencyHz)
            {
                throw new ArgumentOutOfRangeException(nameof(frequencyHz),
                    $"Frequency must be in range 1-{MaxFrequencyHz} Hz.");
            }

            var buf = new byte[ReportLength];
            buf[0] = ReportId;

            if (Model == LbeModel.Lbe1420)
            {
                buf[1] = CmdSetF1Temp;
                buf[2] = (byte)((frequencyHz >> 0) & 0xff);
                buf[3] = (byte)((frequencyHz >> 8) & 0xff);
                buf[4] = (byte)((frequencyHz >> 16) & 0xff);
                buf[5] = (byte)((frequencyHz >> 24) & 0xff);
            }
            else
            {
                if (output == 1)
                {
                    buf[1] = CmdSetF1Temp;
                }
                else
                {
                    buf[1] = CmdSetF2Temp;
                }
                buf[6] = (byte)((frequencyHz >> 0) & 0xff);
                buf[7] = (byte)((frequencyHz >> 8) & 0xff);
                buf[8] = (byte)((frequencyHz >> 16) & 0xff);
                buf[9] = (byte)((frequencyHz >> 24) & 0xff);
            }

            SendFeatureReport(buf);
        }

        /// <summary>
        /// Enables or disables outputs.
        /// LBE-1420 only has OUT1; LBE-1421 controls both.
        /// </summary>
        public void SetOutputsEnabled(bool enabled)
        {
            ThrowIfDisposed();
            EnsureConnected();

            var buf = new byte[ReportLength];
            buf[0] = ReportId;
            buf[1] = CmdEnableOutputs;
            buf[2] = enabled
                ? (byte)(Model == LbeModel.Lbe1421 ? 0x03 : 0x01)
                : (byte)0x00;

            SendFeatureReport(buf);
        }

        /// <summary>
        /// Sets PLL(0) or FLL(1) mode.
        /// </summary>
        public void SetPllMode(bool fllMode)
        {
            ThrowIfDisposed();
            EnsureConnected();

            var buf = new byte[ReportLength];
            buf[0] = ReportId;
            buf[1] = CmdSetPllMode;
            buf[2] = fllMode ? (byte)0x01 : (byte)0x00;

            SendFeatureReport(buf);
        }

        /// <summary>
        /// Enables or disables 1PPS on OUT1 (LBE-1421 only feature).
        /// </summary>
        public void SetPpsEnabled(bool enabled)
        {
            ThrowIfDisposed();
            EnsureConnected();

            if (Model == LbeModel.Lbe1420)
            {
                throw new InvalidOperationException("1PPS control is only supported on LBE-1421.");
            }

            var buf = new byte[ReportLength];
            buf[0] = ReportId;
            buf[1] = CmdSetPps;
            buf[2] = enabled ? (byte)0x01 : (byte)0x00;

            SendFeatureReport(buf);
        }

        /// <summary>
        /// Sets power level for a given output: low power or normal.
        /// For LBE-1420, only OUT1 is valid.
        /// </summary>
        public void SetPowerLevel(int output, bool lowPower)
        {
            ThrowIfDisposed();
            EnsureConnected();

            if (output != 1 && output != 2)
            {
                throw new ArgumentOutOfRangeException(nameof(output), "Output must be 1 or 2.");
            }

            if (Model == LbeModel.Lbe1420 && output != 1)
            {
                throw new InvalidOperationException("LBE-1420 only supports output 1.");
            }

            var buf = new byte[ReportLength];
            buf[0] = ReportId;
            buf[1] = output == 1 ? CmdSetPwr1 : CmdSetPwr2;
            buf[2] = lowPower ? (byte)0x01 : (byte)0x00;

            SendFeatureReport(buf);
        }

        /// <summary>
        /// Blink device LED(s) for identification.
        /// </summary>
        public void BlinkLeds()
        {
            ThrowIfDisposed();
            EnsureConnected();

            var buf = new byte[ReportLength];
            buf[0] = ReportId;
            buf[1] = CmdBlinkLeds;

            SendFeatureReport(buf);
        }

        public LbeStatus GetStatus()
        {
            ThrowIfDisposed();
            EnsureConnected();

            var buf = new byte[ReportLength];
            buf[0] = ReportId;

            // Feature GET report; HidSharp exposes GetFeature directly.
            lock (sync)
            {
                EnsureConnected();
                stream!.GetFeature(buf);
            }

            byte rawStatus;
            uint f1;
            uint f2;
            bool outputsEnabled;
            bool fllEnabled;
            bool pllLocked;
            bool antennaOk;
            bool ppsEnabled;
            bool out1Low;
            bool out2Low;

            if (Model == LbeModel.Lbe1420)
            {
                // Linux/windows mappings differ slightly; we follow the Linux layout as common
                // buf[0] = ReportId
                rawStatus = buf[1];
                f1 = (uint)(buf[6] | (buf[7] << 8) | (buf[8] << 16) | (buf[9] << 24));
                f2 = 0;

                outputsEnabled = true; // seems always on in reference impl
                fllEnabled = buf[18] != 0;
                pllLocked = (rawStatus & StatusPllLockBit) != 0;
                antennaOk = (rawStatus & StatusAntOkBit) != 0;
                ppsEnabled = false;
                out1Low = buf[10] != 0;
                out2Low = false;
            }
            else
            {
                // LBE-1421 dual output (based on windows mapping)
                rawStatus = buf[2];
                f1 = (uint)(buf[7] | (buf[8] << 8) | (buf[9] << 16) | (buf[10] << 24));
                f2 = (uint)(buf[15] | (buf[16] << 8) | (buf[17] << 16) | (buf[18] << 24));

                outputsEnabled = (rawStatus & (StatusOut1EnBit | StatusOut2EnBit)) ==
                                  (StatusOut1EnBit | StatusOut2EnBit);
                fllEnabled = buf[19] != 0;
                pllLocked = (rawStatus & StatusPllLockBit) != 0;
                antennaOk = (rawStatus & StatusAntOkBit) != 0;
                ppsEnabled = (rawStatus & StatusPpsEnBit) != 0;
                out1Low = buf[20] != 0;
                out2Low = buf[21] != 0;
            }

            return new LbeStatus(
                rawStatus,
                f1,
                f2,
                outputsEnabled,
                fllEnabled,
                pllLocked,
                antennaOk,
                ppsEnabled,
                out1Low,
                out2Low);
        }

        private void SendFeatureReport(byte[] buffer)
        {
            if (buffer.Length != ReportLength)
            {
                throw new ArgumentException($"Report buffer must be exactly {ReportLength} bytes.", nameof(buffer));
            }

            lock (sync)
            {
                EnsureConnected();
                stream!.SetFeature(buffer);
            }
        }

        private void EnsureConnected()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("LBE-1421 is not connected. Call Connect() first.");
            }
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(LBE142x));
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Disconnect();
            GC.SuppressFinalize(this);
        }

        ~LBE142x()
        {
            Dispose();
        }
    }

    public enum LbeModel
    {
        Lbe1420 = 0,
        Lbe1421 = 1
    }

    public sealed class LbeStatus
    {
        public byte RawStatus { get; }
        public uint Frequency1Hz { get; }
        public uint Frequency2Hz { get; }
        public bool OutputsEnabled { get; }
        public bool FllEnabled { get; }
        public bool PllLocked { get; }
        public bool AntennaOk { get; }
        public bool PpsEnabled { get; }
        public bool Output1LowPower { get; }
        public bool Output2LowPower { get; }

        public LbeStatus(
            byte rawStatus,
            uint frequency1Hz,
            uint frequency2Hz,
            bool outputsEnabled,
            bool fllEnabled,
            bool pllLocked,
            bool antennaOk,
            bool ppsEnabled,
            bool output1LowPower,
            bool output2LowPower)
        {
            RawStatus = rawStatus;
            Frequency1Hz = frequency1Hz;
            Frequency2Hz = frequency2Hz;
            OutputsEnabled = outputsEnabled;
            FllEnabled = fllEnabled;
            PllLocked = pllLocked;
            AntennaOk = antennaOk;
            PpsEnabled = ppsEnabled;
            Output1LowPower = output1LowPower;
            Output2LowPower = output2LowPower;
        }
    }
}