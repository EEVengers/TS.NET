using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TS.NET.JTAG;

public sealed record FpgaDeviceInfo(int ChainIndex, uint IdCode, string Manufacturer, string Model);

// There's only one driver so don't overcomplicate with interfaces & only supports a subset of XC7A FPGAs
public sealed class Jtag : IDisposable
{
    private const ushort Xc7BypassInstruction = 0x3F;
    private const int MaxScanDevices = 32;
    private const byte SupportedFlashManufacturerId = 0xC2;
    private const byte SupportedFlashMemoryTypeId = 0x20;

    private static readonly Dictionary<int, string> Manufacturer = new()
    {
        [0x049] = "Xilinx",
    };

    private static readonly Dictionary<uint, string> Model = new()
    {
        [0x0362D093] = "XC7A35T",
        [0x0362C093] = "XC7A50T"
    };

    private static readonly Dictionary<string, string> ProxyBitfiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["XC7A35T"] = "bscan_spi_xc7a35t.bit",
        ["XC7A50T"] = "bscan_spi_xc7a50t.bit"
    };

    private readonly ILogger logger;
    private readonly Lock sync = new();
    private readonly XilinxInstructionSet instructionSet;
    private readonly D2xxMpsseDevice mpsse;
    private readonly D2xxJtagTapController tap;
    private bool disposed;

    public Jtag(ILogger logger, int d2xxDeviceIndex = 0, uint ioTimeoutMs = 5000, uint jtagClockHz = 10_000_000, ushort layoutValue = 0x00E8, ushort layoutDirection = 0x60EB)
    {
        this.logger = logger ?? NullLogger.Instance;
        instructionSet = XilinxInstructionSet.XC7;
        mpsse = D2xxMpsseDevice.OpenViaD2xx(d2xxDeviceIndex, ioTimeoutMs, jtagClockHz, layoutValue, layoutDirection);
        tap = new D2xxJtagTapController(mpsse);
    }

    public IReadOnlyList<FpgaDeviceInfo> Scan()
    {
        string DecodeManufacturer(uint idCode)
        {
            var manufacturerCode = (int)((idCode >> 1) & 0x7FF);
            return Manufacturer.TryGetValue(manufacturerCode, out var name) ? name : "Unknown";
        }

        string DecodeModel(uint idCode)
        {
            return Model.TryGetValue(idCode, out var modelName) ? modelName : "Unknown";
        }

        lock (sync)
        {
            ThrowIfDisposed();

            var idCodes = tap.ReadChainIdCodes(instructionSet.IrLength, instructionSet.IdCodeInstruction, MaxScanDevices);
            var results = new List<FpgaDeviceInfo>(idCodes.Count);

            for (var i = 0; i < idCodes.Count; i++)
            {
                var idCode = idCodes[i];
                var manufacturer = DecodeManufacturer(idCode);
                var model = DecodeModel(idCode);
                results.Add(new FpgaDeviceInfo(i, idCode, manufacturer, model));
            }

            return results;
        }
    }

    public ulong Dna(int chainIndex)
    {
        // $"0x{dna:X16}"
        static ulong ReadBitsAsUInt64(byte[] source, int startBit, int bitLength)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(bitLength, 64);
            ulong value = 0;
            for (var i = 0; i < bitLength; i++)
            {
                if (BitManipulation.GetBit(source, startBit + i))
                {
                    value |= 1UL << i;
                }
            }
            return value;
        }

        static ulong ReverseBits64(ulong value)
        {
            ulong reversed = 0;
            for (var i = 0; i < 8; i++)
            {
                reversed = (reversed << 8) | BitManipulation.ReverseBits((byte)(value & 0xFF));
                value >>= 8;
            }
            return reversed;
        }

        lock (sync)
        {
            ThrowIfDisposed();

            var idCodes = tap.ReadChainIdCodes(instructionSet.IrLength, instructionSet.IdCodeInstruction, MaxScanDevices);
            ValidateChainIndex(chainIndex, idCodes.Count);

            tap.ResetTap();

            tap.ShiftIrWriteTarget(idCodes.Count, chainIndex, instructionSet.IrLength, instructionSet.IscEnableInstruction, Xc7BypassInstruction);
            tap.RunIdleCycles(instructionSet.DnaPreReadIdleClocks);

            tap.ShiftIrWriteTarget(idCodes.Count, chainIndex, instructionSet.IrLength, instructionSet.DnaInstruction, Xc7BypassInstruction);
            var payload = new byte[(instructionSet.DnaReadBitLength + 7) / 8];
            var dnaRead = tap.ShiftDrReadWriteTarget(idCodes.Count, chainIndex, payload, instructionSet.DnaReadBitLength);

            tap.RunIdleCycles(instructionSet.DnaPostReadIdleClocks);
            tap.ShiftIrWriteTarget(idCodes.Count, chainIndex, instructionSet.IrLength, instructionSet.IscDisableInstruction, Xc7BypassInstruction);
            tap.RunIdleCycles(instructionSet.DnaPostDisableIdleClocks);

            var raw64 = ReadBitsAsUInt64(dnaRead, 0, 64);
            var rev64 = ReverseBits64(raw64);
            var shift = 64 - instructionSet.DnaBitLength;
            var mask = instructionSet.DnaBitLength == 64 ? ulong.MaxValue : ((1UL << instructionSet.DnaBitLength) - 1);

            return (rev64 >> shift) & mask;
        }
    }

    public uint UserCode(int chainIndex)
    {
        lock (sync)
        {
            ThrowIfDisposed();

            var idCodes = tap.ReadChainIdCodes(instructionSet.IrLength, instructionSet.IdCodeInstruction, MaxScanDevices);
            ValidateChainIndex(chainIndex, idCodes.Count);

            tap.ResetTap();
            tap.ShiftIrWriteTarget(idCodes.Count, chainIndex, instructionSet.IrLength, instructionSet.UserCodeInstruction, Xc7BypassInstruction);

            var payload = new byte[4];
            var read = tap.ShiftDrReadWriteTarget(idCodes.Count, chainIndex, payload, 32);
            var userCode =
                (uint)read[0] |
                ((uint)read[1] << 8) |
                ((uint)read[2] << 16) |
                ((uint)read[3] << 24);

            logger.LogInformation("USERCODE read: 0x{UserCode:X8}", userCode);
            return userCode;
        }
    }

    public void Program(int chainIndex, string imagePath, CancellationToken cancellationToken)
    {
        Action<int> BuildPercentReporter(string label)
        {
            var last = -1;
            return percent =>
            {
                if (percent == last)
                {
                    return;
                }

                last = percent;
                logger.LogInformation("{Label}: {Percent}%", label, percent);
            };
        }

        var image = ReadImage(imagePath);
        var progressReporter = BuildPercentReporter($"Program");

        var configStream = BitfileParser.ExtractConfigurationStream(image);
        BitManipulation.ReverseBitsInPlace(configStream);

        lock (sync)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            logger.LogInformation($"Programming {Path.GetFileName(imagePath)}");

            var idCodes = tap.ReadChainIdCodes(instructionSet.IrLength, instructionSet.IdCodeInstruction, MaxScanDevices);
            ValidateChainIndex(chainIndex, idCodes.Count);

            tap.ShiftIrWriteTarget(idCodes.Count, chainIndex, instructionSet.IrLength, instructionSet.JShutdownInstruction, Xc7BypassInstruction);
            tap.RunIdleCycles(instructionSet.ProgramShutdownIdleClocks);
            cancellationToken.ThrowIfCancellationRequested();

            tap.ShiftIrWriteTarget(idCodes.Count, chainIndex, instructionSet.IrLength, instructionSet.JProgramInstruction, Xc7BypassInstruction);
            cancellationToken.WaitHandle.WaitOne(instructionSet.ProgramPostJProgramDelayMs);
            cancellationToken.ThrowIfCancellationRequested();

            tap.ShiftIrWriteTarget(idCodes.Count, chainIndex, instructionSet.IrLength, instructionSet.CfgInInstruction, Xc7BypassInstruction);
            tap.ShiftDrWriteTarget(idCodes.Count, chainIndex, configStream, configStream.Length * 8, progressReporter);
            cancellationToken.ThrowIfCancellationRequested();

            tap.RunIdleCycles(instructionSet.ProgramPostCfgInIdleClocks);

            tap.ShiftIrWriteTarget(idCodes.Count, chainIndex, instructionSet.IrLength, instructionSet.JStartInstruction, Xc7BypassInstruction);
            tap.RunIdleCycles(instructionSet.PostProgramIdleClocks);

            cancellationToken.WaitHandle.WaitOne(50);
            cancellationToken.ThrowIfCancellationRequested();

            logger.LogInformation("Program complete");
        }
    }

    public void EraseSpiFlash(int chainIndex, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            var spiProxy = ProgramSpiProxy(chainIndex, cancellationToken);
            var flashCapacityBytes = ReadFlashCapacityBytes(spiProxy, cancellationToken);
            logger.LogInformation($"Detected SPI flash with capacity of {(flashCapacityBytes * 8) / 1024 / 1024}Mb");
            if (flashCapacityBytes > 0x01_00_00_00)
            {
                throw new NotSupportedException("Only 24-bit SPI flash addressing is currently supported.");
            }

            var totalSectors = (flashCapacityBytes + instructionSet.FlashSectorSizeBytes - 1) / instructionSet.FlashSectorSizeBytes;
            var erasedSectors = 0;
            var lastErasePercent = -1;

            logger.LogInformation($"Erasing flash ({totalSectors} sectors, {totalSectors * instructionSet.FlashSectorSizeBytes} bytes)");
            LogStepProgress("Flash erase", erasedSectors, totalSectors, ref lastErasePercent);

            for (var sectorAddress = 0; sectorAddress < flashCapacityBytes; sectorAddress += instructionSet.FlashSectorSizeBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                spiProxy.WriteEnable();
                spiProxy.Write(SpiFlashOpcodes.SectorErase, BuildAddress24(sectorAddress));
                spiProxy.WaitWhileBusy(TimeSpan.FromSeconds(1), cancellationToken);

                erasedSectors++;
                LogStepProgress("Flash erase", erasedSectors, totalSectors, ref lastErasePercent);
            }
            logger.LogInformation("Flash erase complete");
        }
    }

    /// <summary>
    /// Note: flash should be erased before programming. (Write once behaviour)
    /// </summary>
    public void ProgramSpiFlash(int chainIndex, string imagePath, CancellationToken cancellationToken)
    {
        List<int> BuildPageProgramList(byte[] image, int usedLength, int pageSize)
        {
            var result = new List<int>();
            for (var pageStart = 0; pageStart < usedLength; pageStart += pageSize)
            {
                var pageEnd = Math.Min(pageStart + pageSize, usedLength);
                var needsWrite = false;
                for (var i = pageStart; i < pageEnd; i++)
                {
                    if (image[i] != 0xFF)
                    {
                        needsWrite = true;
                        break;
                    }
                }

                if (needsWrite)
                {
                    result.Add(pageStart);
                }
            }

            return result;
        }

        var image = ReadImage(imagePath);

        lock (sync)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            var spiProxy = ProgramSpiProxy(chainIndex, cancellationToken);
            var flashCapacityBytes = ReadFlashCapacityBytes(spiProxy, cancellationToken);
            logger.LogInformation($"Detected SPI flash with capacity of {(flashCapacityBytes * 8) / 1024 / 1024}Mb");
            if (image.Length > flashCapacityBytes)
            {
                throw new InvalidOperationException($"Image length ({image.Length} bytes) exceeds detected flash capacity ({flashCapacityBytes} bytes).");
            }

            //EraseSpiFlash(spiProxy, cancellationToken);

            var pagesToProgram = BuildPageProgramList(image, image.Length, instructionSet.FlashPageSizeBytes);
            var programmedPages = 0;
            var lastFlashPercent = -1;

            logger.LogInformation($"Programming flash {Path.GetFileName(imagePath)} ({pagesToProgram.Count} pages, {pagesToProgram.Count * instructionSet.FlashPageSizeBytes} bytes)");
            LogStepProgress("Flash program", programmedPages, pagesToProgram.Count, ref lastFlashPercent);
            foreach (var pageAddress in pagesToProgram)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var count = Math.Min(instructionSet.FlashPageSizeBytes, image.Length - pageAddress);
                var page = new byte[count];
                Buffer.BlockCopy(image, pageAddress, page, 0, count);

                spiProxy.WriteEnable();
                spiProxy.Write(SpiFlashOpcodes.PageProgram, ComposeWritePayload(BuildAddress24(pageAddress), page));
                spiProxy.WaitWhileBusy(TimeSpan.FromSeconds(3), cancellationToken);

                programmedPages++;
                LogStepProgress($"Flash program", programmedPages, pagesToProgram.Count, ref lastFlashPercent);
            }

            logger.LogInformation("Flash program complete");
        }
    }

    public void ProgramSpiFlashSector(int chainIndex, int address, ReadOnlySpan<byte> data, CancellationToken cancellationToken)
    {
        if (data.Length <= 0)
        {
            throw new ArgumentException("Data is required.", nameof(data));
        }

        if (data.Length > instructionSet.FlashSectorSizeBytes)
        {
            throw new ArgumentException($"Data length must be <= {instructionSet.FlashSectorSizeBytes} bytes.", nameof(data));
        }

        if (address < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(address));
        }

        var sectorSize = instructionSet.FlashSectorSizeBytes;
        var sectorStart = address - (address % sectorSize);
        var offsetInSector = address - sectorStart;
        if (offsetInSector + data.Length > sectorSize)
        {
            throw new ArgumentException("Data must fit within a single sector.", nameof(data));
        }

        lock (sync)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            var spiProxy = ProgramSpiProxy(chainIndex, cancellationToken);
            var flashCapacityBytes = ReadFlashCapacityBytes(spiProxy, cancellationToken);

            if ((long)address + data.Length > flashCapacityBytes)
            {
                throw new InvalidOperationException($"Sector patch (address 0x{address:X8}, {data.Length} bytes) exceeds detected flash capacity ({flashCapacityBytes} bytes).");
            }

            var sectorImage = spiProxy.Read(SpiFlashOpcodes.Read, BuildAddress24(sectorStart), sectorSize);
            if (sectorImage.Length != sectorSize)
            {
                throw new InvalidOperationException($"SPI read returned {sectorImage.Length} bytes, expected {sectorSize} bytes.");
            }

            data.CopyTo(sectorImage.AsSpan(offsetInSector));

            spiProxy.WriteEnable();
            spiProxy.Write(SpiFlashOpcodes.SectorErase, BuildAddress24(sectorStart));
            spiProxy.WaitWhileBusy(TimeSpan.FromSeconds(2), cancellationToken);

            for (var pageOffset = 0; pageOffset < sectorSize; pageOffset += instructionSet.FlashPageSizeBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var page = sectorImage.AsSpan(pageOffset, instructionSet.FlashPageSizeBytes);
                var needsWrite = false;
                for (var i = 0; i < page.Length; i++)
                {
                    if (page[i] != 0xFF)
                    {
                        needsWrite = true;
                        break;
                    }
                }

                if (!needsWrite)
                {
                    continue;
                }

                spiProxy.WriteEnable();
                spiProxy.Write(SpiFlashOpcodes.PageProgram, ComposeWritePayload(BuildAddress24(sectorStart + pageOffset), page));
                spiProxy.WaitWhileBusy(TimeSpan.FromSeconds(3), cancellationToken);
            }

            logger.LogInformation($"SPI flash sector programmed at sector 0x{sectorStart:X8}; patched address 0x{address:X8} ({data.Length} bytes)");
        }
    }

    public void VerifySpiFlash(int chainIndex, string imagePath, CancellationToken cancellationToken)
    {
        var image = ReadImage(imagePath);

        lock (sync)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            var spiProxy = ProgramSpiProxy(chainIndex, cancellationToken);
            var flashCapacityBytes = ReadFlashCapacityBytes(spiProxy, cancellationToken);
            logger.LogInformation($"Detected SPI flash with capacity of {(flashCapacityBytes * 8) / 1024 / 1024}Mb");
            if (image.Length > flashCapacityBytes)
            {
                throw new InvalidOperationException($"Image length ({image.Length} bytes) exceeds detected flash capacity ({flashCapacityBytes} bytes).");
            }

            const int readChunkSize = 1024;
            var verified = 0;
            var lastPercent = -1;

            logger.LogInformation($"Verifying flash against {Path.GetFileName(imagePath)} ({image.Length} bytes)");
            LogStepProgress("Flash verify", verified, image.Length, ref lastPercent);
            while (verified < image.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var count = Math.Min(readChunkSize, image.Length - verified);
                var read = spiProxy.Read(SpiFlashOpcodes.Read, BuildAddress24(verified), count);
                if (read.Length != count)
                {
                    throw new InvalidOperationException($"SPI read returned {read.Length} bytes, expected {count} bytes.");
                }

                for (var i = 0; i < count; i++)
                {
                    if (read[i] != image[verified + i])
                    {
                        var address = verified + i;
                        throw new InvalidDataException($"Flash verify failed at address 0x{address:X8}: expected 0x{image[address]:X2}, actual 0x{read[i]:X2}.");
                    }
                }

                verified += count;
                LogStepProgress("Flash verify", verified, image.Length, ref lastPercent);
            }

            logger.LogInformation("Flash verify complete");
        }
    }

    public void Reset(int chainIndex, CancellationToken cancellationToken)
    {
        lock (sync)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();

            var idCodes = tap.ReadChainIdCodes(instructionSet.IrLength, instructionSet.IdCodeInstruction, MaxScanDevices);
            ValidateChainIndex(chainIndex, idCodes.Count);

            logger.LogInformation("Reset FPGA");
            tap.ShiftIrWriteTarget(idCodes.Count, chainIndex, instructionSet.IrLength, instructionSet.JProgramInstruction, Xc7BypassInstruction);
            cancellationToken.WaitHandle.WaitOne(instructionSet.ProgramPostJProgramDelayMs);
            cancellationToken.ThrowIfCancellationRequested();

            tap.RunIdleCycles(instructionSet.PostProgramIdleClocks);
        }
    }

    private JtagSpiProxy ProgramSpiProxy(int chainIndex, CancellationToken cancellationToken)
    {
        static bool IsProbeFailure(Exception ex)
        {
            return ex is InvalidOperationException || ex is InvalidDataException;
        }

        static string ResolveProxyBitfilePath(string model)
        {
            if (!ProxyBitfiles.TryGetValue(model, out var proxyName))
            {
                throw new NotSupportedException($"No proxy bitfile mapping exists for model '{model}'.");
            }

            var baseDir = AppContext.BaseDirectory;
            var candidates = new[]
            {
            Path.Combine(baseDir, "Bitfiles", proxyName),
            Path.Combine(Directory.GetCurrentDirectory(), "Bitfiles", proxyName),
        };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            throw new FileNotFoundException($"Could not locate proxy bitfile '{proxyName}' under a 'bitfiles' folder.");
        }

        var devices = Scan();
        ValidateChainIndex(chainIndex, devices.Count);

        var existingProxy = OpenSpiProxy(chainIndex);
        try
        {
            ReadFlashCapacityBytes(existingProxy, cancellationToken);
            logger.LogInformation("SPI proxy detected; SPI proxy programming skipped");
            return existingProxy;
        }
        catch (Exception ex) when (IsProbeFailure(ex))
        {
            logger.LogInformation("SPI proxy not detected; programming SPI proxy");
        }

        var target = devices[chainIndex];
        var proxyPath = ResolveProxyBitfilePath(target.Model);
        Program(chainIndex, proxyPath, cancellationToken);

        var spiProxy = OpenSpiProxy(chainIndex);
        ReadFlashCapacityBytes(spiProxy, cancellationToken);
        logger.LogInformation("SPI proxy programmed and verified");

        return spiProxy;
    }

    private JtagSpiProxy OpenSpiProxy(int chainIndex)
    {
        var idCodes = tap.ReadChainIdCodes(instructionSet.IrLength, instructionSet.IdCodeInstruction, MaxScanDevices);
        ValidateChainIndex(chainIndex, idCodes.Count);

        tap.ShiftIrWriteTarget(idCodes.Count, chainIndex, instructionSet.IrLength, instructionSet.User1Instruction, Xc7BypassInstruction);

        return new JtagSpiProxy(tap, idCodes.Count, chainIndex);
    }

    private int ReadFlashCapacityBytes(JtagSpiProxy jtagSpiProxy, CancellationToken cancellationToken)
    {
        static bool TryDecodeCapacityBytes(byte capacityCode, out int capacityBytes)
        {
            capacityBytes = 0;
            if (capacityCode >= 31)
            {
                return false;
            }

            var bytes = 1L << capacityCode;
            if (bytes <= 0 || bytes > int.MaxValue)
            {
                return false;
            }

            capacityBytes = (int)bytes;
            return true;
        }

        jtagSpiProxy.Write(SpiFlashOpcodes.ReleasePowerDown, ReadOnlySpan<byte>.Empty);
        cancellationToken.WaitHandle.WaitOne(2);
        cancellationToken.ThrowIfCancellationRequested();

        var id = jtagSpiProxy.Read(SpiFlashOpcodes.ReadId, ReadOnlySpan<byte>.Empty, 3);
        if (id.Length < 3)
        {
            throw new InvalidDataException("Invalid ID length.");
        }

        if (id[0] != SupportedFlashManufacturerId)
        {
            throw new InvalidDataException($"Unsupported flash manufacturer ID 0x{id[0]:X2}. Only 0x{SupportedFlashManufacturerId:X2} is currently supported.");
        }

        if (id[1] != SupportedFlashMemoryTypeId)
        {
            throw new InvalidDataException($"Unsupported flash memory type ID 0x{id[1]:X2} for manufacturer 0x{SupportedFlashManufacturerId:X2}. Only 0x{SupportedFlashMemoryTypeId:X2} is currently supported.");
        }

        if (!TryDecodeCapacityBytes(id[2], out var capacityBytes) || capacityBytes < 64 * 1024)
        {
            throw new InvalidDataException($"Unsupported flash capacity code 0x{id[2]:X2}.");
        }

        return capacityBytes;
    }

    private static byte[] BuildAddress24(int address)
    {
        return [(byte)(address >> 16), (byte)(address >> 8), (byte)address,];
    }

    private static byte[] ComposeWritePayload(byte[] prefix, ReadOnlySpan<byte> data)
    {
        var result = new byte[prefix.Length + data.Length];
        Buffer.BlockCopy(prefix, 0, result, 0, prefix.Length);
        data.CopyTo(result.AsSpan(prefix.Length));
        return result;
    }

    private byte[] ReadImage(string imagePath)
    {
        static byte[] ReadFlashImage(string imagePath)
        {
            var source = File.ReadAllBytes(imagePath);
            var ext = Path.GetExtension(imagePath);
            if (ext.Equals(".hex", StringComparison.OrdinalIgnoreCase) || ext.Equals(".mcs", StringComparison.OrdinalIgnoreCase))
            {
                throw new NotImplementedException("Only binary image files supported");
            }

            return source;
        }

        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Flash image path is required.", nameof(imagePath));
        }

        var fullPath = Path.GetFullPath(imagePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("Flash image not found.", fullPath);
        }

        var image = ReadFlashImage(fullPath);
        if (image.Length > 0x01_00_00_00)
        {
            throw new NotSupportedException("Only 24-bit SPI flash addressing is currently supported.");
        }
        return image;
    }

    private void LogStepProgress(string label, int done, int total, ref int lastPercent)
    {
        if (total <= 0)
        {
            if (lastPercent == 100)
            {
                return;
            }

            lastPercent = 100;
            logger.LogInformation("{Label}: 100%", label);
            return;
        }

        var value = (int)Math.Clamp((long)done * 100 / total, 0, 100);
        if (value == lastPercent)
        {
            return;
        }

        lastPercent = value;
        logger.LogInformation("{Label}: {Percent}%", label, value);
    }

    private static void ValidateChainIndex(int chainIndex, int count)
    {
        if (count <= 0)
        {
            throw new InvalidOperationException("No JTAG devices were found on the chain.");
        }

        if (chainIndex < 0 || chainIndex >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(chainIndex), $"Valid chain index range is 0..{count - 1}.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(Jtag));
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
            {
                return;
            }

            mpsse.Dispose();
            disposed = true;
        }
    }
}