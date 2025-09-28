namespace TS.NET.Photino;

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
