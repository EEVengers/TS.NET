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

            if (!File.Exists(variables.FpgaFlashImagePath))
            {
                Logger.Instance.Log(LogLevel.Error, Index, Status.Error, $"FPGA flash image not found: {variables.FpgaFlashImagePath}");
                return Status.Error;
            }
            jtag.ProgramSpiFlash(0, variables.FpgaFlashImagePath, cancellationToken);
            Result!.Summary = Path.GetFileName(variables.FpgaFlashImagePath);
            return Status.Passed;
        };
    }
}
