using TS.NET.JTAG;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class JtagReadDnaStep : Step
{
    public JtagReadDnaStep(string name, FactoryBringUpVariables variables) : base(name)
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

            var dna = jtag.Dna(0);
            variables.FpgaDna = dna;
            Logger.Instance.Log(LogLevel.Information, Index, Status.Passed, $"DNA: 0x{dna:X16}");

            //var userCode = jtag.UserCode(0);
            //Logger.Instance.Log(LogLevel.Information, Index, Status.Passed, $"USERCODE: 0x{userCode:X8}");
            Result!.Summary = $"0x{variables.FpgaDna:X16}";
            return Status.Passed;
        };
    }
}
