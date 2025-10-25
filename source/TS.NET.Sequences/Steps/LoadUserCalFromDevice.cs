using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class LoadUserCalFromDeviceStep : Step
{
    public LoadUserCalFromDeviceStep(string name, CommonVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            if(Instruments.Instance.TryReadUserCalibration(out var calibration))
            {
                variables.Calibration = calibration!;
                Logger.Instance.Log(LogLevel.Information, Index, Status.Done, $"Calibration loaded from device");
                return Status.Done;
            }        

            Logger.Instance.Log(LogLevel.Error, Index, Status.Error, $"Calibration not found in device or file");
            return Status.Error;
        };
    }
}
