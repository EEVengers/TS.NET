using TS.NET.Sequencer;

namespace TS.NET.Testbench.UI;

internal class SequenceStatusUpdateDto : MessageDto
{
    public Status? Status { get; set; }
}
