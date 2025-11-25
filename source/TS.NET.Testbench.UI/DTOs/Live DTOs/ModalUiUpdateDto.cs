namespace TS.NET.Testbench.UI;

public class ModalUiUpdateDto : MessageDto
{
    public required string Html {get;set;}
    public required string? Script {get;set;}
}
