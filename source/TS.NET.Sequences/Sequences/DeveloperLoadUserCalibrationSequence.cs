using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class DeveloperLoadUserCalibrationSequence : Sequence
{
    public FactoryVariables Variables { get; private set; }

    public DeveloperLoadUserCalibrationSequence(ModalUiContext modalUiContext, FactoryVariables variables)
    {
        Name = "Developer load user calibration";
        Version = "1.0";
        Variables = variables;
        AddSteps(modalUiContext);
        SetStepIndices();
    }

    private void AddSteps(ModalUiContext modalUiContext)
    {
        Steps =
        [
            new InitialiseDeviceStep("Initialise device", Variables),
            new Step("Discover calibration file"){ Action = (CancellationToken cancellationToken) => {
                var calibrationFiles = Directory.GetFiles(Directory.GetCurrentDirectory(), "thunderscope-calibration*.json");
                if (calibrationFiles.Length != 1)
                {
                    Logger.Instance.Log(LogLevel.Error, $"Expected exactly one thunderscope-calibration*.json file in the current directory, found {calibrationFiles.Length}.");
                    return Sequencer.Status.Failed;
                }

                Variables.CalibrationFileName = calibrationFiles[0];
                return Sequencer.Status.Done;
            }},
            new LoadCalibrationFromFileStep("Load calibration from file", Variables),

            new SaveUserCalToDeviceStep("Save user calibration to device", Variables) { AllowSkip = true },

            new Step("Cleanup"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.Close();
                return Sequencer.Status.Done;
            }},
        ];
    }
}
