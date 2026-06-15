using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class DeveloperHwidSequence : Sequence
{
    public CommonVariables Variables { get; private set; }

    public DeveloperHwidSequence(ModalUiContext modalUiContext, CommonVariables variables)
    {
        Name = "Developer HWID";
        Version = "1.0";
        Variables = variables;
        AddSteps(modalUiContext);
        SetStepIndices();
    }

    private void AddSteps(ModalUiContext modalUiContext)
    {
        Steps =
        [
            new ModalDialogStep("Erase check", modalUiContext)
            {
                Title = "Erase check",
                Message = "This sequence will erase the factory data area and append a HWID tag. Do you wish to continue?",
                Buttons = DialogButtons.YesNo,
                Icon = DialogIcon.Question
            },
            new InitialiseDeviceStep("Initialise device", Variables),
            new BetaTesterHwidStep("Enter HWID", modalUiContext, Variables),
            new Step("Cleanup"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.Close();
                return Sequencer.Status.Done;
            }},
        ];
    }
}
