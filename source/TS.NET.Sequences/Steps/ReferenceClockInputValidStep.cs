using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class ReferenceClockInputValidStep : Step
{
    public ReferenceClockInputValidStep(string name, CalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            var refClockInValid = Instruments.Instance.GetThunderscopeRefClockInValid();
            if(refClockInValid)
            {
                Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Reference clock input is valid");
                return Status.Passed;
            }
            else
            {
                Logger.Instance.Log(LogLevel.Error, Index, Status.Error, $"Reference clock input is invalid");
                return Status.Failed;
            }
        };
    }
}
