using TS.NET.Sequencer;

namespace TS.NET.Calibration.UI
{
    internal class SequenceStatusUpdateDto : MessageDto
    {
        public Status? Status { get; set; }
    }
}
