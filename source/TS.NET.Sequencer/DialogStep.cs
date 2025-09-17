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
            var dialogResult = uiDialog?.Invoke(new Dialog { Title = Title, Text = Text, Buttons = Buttons, Icon = Icon });
            if (dialogResult == DialogResult.Ok || dialogResult == DialogResult.Yes)
                return Status.Done;
            else
                return Status.Error;
        };
    }
}

public enum DialogIcon
{
    Info,
    Warning,
    Error,
    Question
}

public enum DialogButtons
{
    Ok,
    OkCancel,
    YesNo,
    YesNoCancel,
    RetryCancel,
    AbortRetryIgnore
}

public enum DialogResult
{
    Cancel = -1,
    Ok,
    Yes,
    No,
    Abort,
    Retry,
    Ignore
}

public class Dialog
{
    public required string Title { get; set; }
    public required string Text { get; set; }
    public DialogButtons Buttons { get; set; }
    public DialogIcon Icon { get; set; }
}
