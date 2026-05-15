using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class FactoryBringUpSequence : Sequence
{
    public FactoryBringUpVariables Variables { get; private set; }

    public FactoryBringUpSequence(ModalUiContext modalUiContext, FactoryBringUpVariables variables)
    {
        Name = "Factory bring-up";
        Variables = variables;
        AddSteps(modalUiContext);
        SetStepIndices();
    }

    private void AddSteps(ModalUiContext modalUiContext)
    {
        Steps =
        [
            new ModalDialogStep("PCB check", modalUiContext)
            {
                Title = "PCB check",
                Message = "PCB powered up with JTAG-HS2 plugged in, and no USB/BNC connections?",
                Buttons = DialogButtons.YesNo,
                Icon = DialogIcon.Question
            },

            new JtagScanStep("JTAG scan", Variables),
            new JtagReadDnaStep("JTAG read DNA", Variables),

            new FactoryHwidStep("HWID input", modalUiContext, Variables),

            new JtagEraseSpiFlashStep("JTAG erase SPI flash", Variables),
            new JtagProgramSpiFlashStep("JTAG program SPI flash", Variables),
            new JtagVerifySpiFlashStep("JTAG verify SPI flash", Variables),
            new JtagProgramHwidStep("JTAG program HWID", Variables),
            new JtagResetFpga("JTAG reset FPGA", Variables),

            new Step("Cleanup"){ Action = (CancellationToken cancellationToken) => {
                return Sequencer.Status.Done;
            }},
        ];
    }
}
