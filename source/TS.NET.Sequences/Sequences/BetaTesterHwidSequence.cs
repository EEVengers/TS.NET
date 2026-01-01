using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class BetaTesterHwidSequence : Sequence
{
    public CommonVariables Variables { get; private set; }

    public BetaTesterHwidSequence(ModalUiContext modalUiContext, CommonVariables variables)
    {
        Name = "Beta tester HWID";
        Variables = variables;
        AddSteps(modalUiContext);
        SetStepIndices();
    }

    private void AddSteps(ModalUiContext modalUiContext)
    {
        Steps =
        [
            new InitialiseDeviceStep("Initialise device", Variables),
            new BetaTesterHwidStep("Enter HWID", modalUiContext, Variables),
            new Step("Cleanup"){ Action = (CancellationToken cancellationToken) => {
                Instruments.Instance.Close();
                return Sequencer.Status.Done;
            }},
        ];
    }
}
