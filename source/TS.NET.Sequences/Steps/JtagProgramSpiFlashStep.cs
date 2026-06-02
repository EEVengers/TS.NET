using TS.NET.JTAG;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class JtagProgramSpiFlashStep : Step
{
    public JtagProgramSpiFlashStep(string name, FactoryBringUpVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            using var jtag = new Jtag(new SequencerLoggerAdapter(Index));
            var devices = jtag.Scan();

            if (devices.Count == 0)
            {
                Logger.Instance.Log(LogLevel.Error, Index, Status.Error, $"No FPGA found");
                return Status.Error;
            }

            if (devices.Count > 1)
            {
                Logger.Instance.Log(LogLevel.Error, Index, Status.Error, $"More than one FPGA found");
                return Status.Error;
            }

            if(string.IsNullOrWhiteSpace(variables.FpgaModel))
            {
                Logger.Instance.Log(LogLevel.Error, Index, Status.Error, $"FPGA model not specified");
                return Status.Error;
            }

            var flashImage = Path.Combine("Bitfiles", variables.FlashImages[variables.FpgaModel]);

            if (!File.Exists(flashImage))
            {
                Logger.Instance.Log(LogLevel.Error, Index, Status.Error, $"FPGA flash image not found: {flashImage}");
                return Status.Error;
            }

            jtag.ProgramSpiFlash(0, flashImage, cancellationToken);
            Result!.Summary = Path.GetFileName(flashImage);
            return Status.Passed;
        };
    }
}
