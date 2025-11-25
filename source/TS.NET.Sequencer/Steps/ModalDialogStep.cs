using System.Text.Json;

namespace TS.NET.Sequencer;

public class ModalDialogStep : ModalUiStep
{
    public required string Title { get; set; }
    public required string Message { get; set; }
    public required DialogButtons Buttons { get; set; }
    public required DialogIcon Icon { get; set; }

    private bool continueLoop = false;

    public ModalDialogStep(string name, ModalUiContext modalUiContext) : base(name, modalUiContext)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            RegisterEventHandler((JsonElement eventData) =>
            {
                if (eventData.TryGetProperty("buttonClicked", out var buttonClicked))
                {
                    if (buttonClicked.GetString() == "ok")
                    {
                        continueLoop = false;
                    }
                    else if (buttonClicked.GetString() == "cancel")
                    {
                        continueLoop = false;
                    }
                }
            });

            UpdateUi<ModalDialog>(new Dictionary<string, object?>()
            {
                { "Title", Title},
                { "Message", Message }
            });

            continueLoop = true;
            while(continueLoop)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(100);
            }

            HideUi();

            return Status.Done;
        };
    }
}
