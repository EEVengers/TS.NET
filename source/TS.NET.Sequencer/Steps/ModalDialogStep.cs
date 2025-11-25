using System.Text.Json;

namespace TS.NET.Sequencer;

public class ModalDialogStep : ModalUiStep
{
    public required string Title { get; set; }
    public required string Message { get; set; }
    public required DialogButtons Buttons { get; set; }
    public required DialogIcon Icon { get; set; }

    private Status status = Status.Running;
    private bool continueLoop = false;

    public ModalDialogStep(string name, ModalUiContext modalUiContext) : base(name, modalUiContext)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            RegisterEventHandler((JsonElement eventData) =>
            {
                if (eventData.TryGetProperty("buttonClicked", out var buttonClicked))
                {
                    switch (buttonClicked.GetString())
                    {
                        case "ok":
                        case "yes":
                            status = Status.Done;
                            break;
                        case "cancel":
                        case "no":
                            status = Status.Cancelled;
                            break;

                    }
                    continueLoop = false;
                }
            });

            UpdateUi<ModalDialog>(new Dictionary<string, object?>()
            {
                { "Title", Title},
                { "Message", Message },
                { "Buttons", Buttons },
                { "Icon", Icon }
            });

            continueLoop = true;
            while (continueLoop)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(100);
            }

            HideUi();

            return status;
        };
    }
}
