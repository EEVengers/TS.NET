namespace TS.NET.Calibration.UI;

public class DialogDto : MessageDto
{
    public string? Title { get; set; }
    public string? Message { get; set; }
    public bool StringInput { get; set; }
    public string? Button1 { get; set; }
    public string? Button2 { get; set; }
}
