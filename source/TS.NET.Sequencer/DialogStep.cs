using TS.NET.Photino;

namespace TS.NET.Sequencer;

public class DialogStep : Step
{
    public required string Title { get; set; }
    public required string Text { get; set; }
    public required DialogButtons Buttons { get; set; }
    public required DialogIcon Icon { get; set; }

    public DialogStep(string name, Func<Dialog, DialogResult> uiDialog) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            var dialogResult = uiDialog?.Invoke(new Dialog { Title = Title!, Text = Text!, Buttons = Buttons, Icon = Icon });
            Result!.Summary = $"Operator clicked {dialogResult}";
            if (dialogResult == DialogResult.Ok || dialogResult == DialogResult.Yes)
                return Status.Done;
            else
                return Status.Error;
        };
    }
}
