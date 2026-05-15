using TS.NET.JTAG;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class JtagProgramHwidStep : Step
{
    public JtagProgramHwidStep(string name, FactoryBringUpVariables variables) : base(name)
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

            // A50T/Dev/Prod - Factory Calibration Data: 0x280000
            // A100T/A200T - Factory Calibration Data: 0x0B00000
            var bytes = ThunderscopeNonVolatileMemory.BuildHwidTLV(variables.Hwid);
            const int baseAddress = 0x280000;
            jtag.ProgramSpiFlashSector(0, baseAddress, bytes, cancellationToken);
            return Status.Passed;
        };
    }
}
