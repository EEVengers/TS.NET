using TS.NET.Sequencer;

namespace TS.NET.Testbench.UI;

public class ModalUiUpdateDto : MessageDto
{
    public required string? Html { get; set; }
    public required string? Script { get; set; }

    internal static ModalUiUpdateDto FromModalUiUpdate(ModalUiUpdate modalUiUpdate)
    {
        return new ModalUiUpdateDto
        {
            Type = "modal-ui-update",
            Html = modalUiUpdate.Html,
            Script = modalUiUpdate.Script
        };
    }
}
