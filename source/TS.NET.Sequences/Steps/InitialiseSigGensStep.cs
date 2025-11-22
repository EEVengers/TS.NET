using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class InitialiseSigGensStep : Step
{
    public InitialiseSigGensStep(string name, BenchCalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            SigGens.Instance.Initialise(variables.SigGen1Host, variables.SigGen2Host);
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Instruments initialised");
            return Status.Done;
        };
    }
}
