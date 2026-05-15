using TS.NET.JTAG;
using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class JtagScanStep : Step
{
    public JtagScanStep(string name, FactoryBringUpVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            using var jtag = new Jtag(new SequencerLoggerAdapter(Index));
            var devices = jtag.Scan();

            if(devices.Count == 0)
            {
                Logger.Instance.Log(LogLevel.Error, Index, Status.Error, $"No FPGA found");
                return Status.Error;
            }

            if (devices.Count > 1)
            {
                Logger.Instance.Log(LogLevel.Error, Index, Status.Error, $"More than one FPGA found");
                return Status.Error;
            }
            
            var fpga = devices[0];
            Logger.Instance.Log(LogLevel.Information, Index, Status.Passed, $"Found FPGA: {fpga.Model}");
            variables.FpgaModel = fpga.Model;

            if (fpga.Model != "XC7A35T" && fpga.Model != "XC7A50T" )
            {
                Logger.Instance.Log(LogLevel.Error, Index, Status.Error, $"Incorrect FPGA found");
                return Status.Error;
            }

            Result!.Summary = fpga.Model;
            return Status.Passed;
        };
    }
}
