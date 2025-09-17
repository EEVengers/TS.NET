using TS.NET.Sequencer;

namespace TS.NET.Calibration;

public class InitialiseInstrumentsStep : Step
{
    public InitialiseInstrumentsStep(string name, bool initSigGens) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.Initialise(initSigGens);
            Instruments.Instance.SetThunderscopeChannel([0]);
            Instruments.Instance.SetThunderscopeRate(1_000_000_000);
            Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Instruments initialised");
            return Status.Done;
        };
    }
}
