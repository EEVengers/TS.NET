using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class TrimOffsetDacZeroStep : Step
{
    public TrimOffsetDacZeroStep(string name, int channelIndex, int pathIndex, CalibrationVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            Instruments.Instance.SetThunderscopeRate(1_000_000_000, variables);
            var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, pathIndex, variables);
            var pathConfig = Utility.GetChannelPathConfig(channelIndex, pathIndex, variables);
            DebugLog.Instance.Log($"TrimOffsetDacZeroStep, channelIndex: {channelIndex}, pathIndex: {pathIndex}");

            var high = pathConfig.TargetDPotResolution * 0.8;
            var low = pathConfig.TargetDPotResolution * -0.8;

            bool solutionFound = false;
            for (int i = 0; i < 50 && !solutionFound; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Instruments.Instance.SetThunderscopeCalManual50R(channelIndex, pathCalibration.TrimOffsetDacZero, pathCalibration.TrimScaleDac, pathCalibration.PgaPreampGain, pathCalibration.PgaLadderAttenuator, variables);
                var average = Instruments.Instance.GetThunderscopeAverage(channelIndex);

                if (average > high)
                    pathCalibration.TrimOffsetDacZero++;
                if (average < low)
                    pathCalibration.TrimOffsetDacZero--;
                if (average >= low && average <= high)
                {
                    variables.ParametersSet++;
                    Logger.Instance.Log(LogLevel.Information, Index, Status.Passed, $"Zero: {pathCalibration.TrimOffsetDacZero} | Average: {average}");
                    return Status.Passed;
                }
            }
            if (!solutionFound)
            {
                throw new TestbenchException("Could not converge");
            }
            return Status.Passed;
        };
    }
}
