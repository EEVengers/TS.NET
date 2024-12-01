namespace TS.NET
{
    // Notes
    //
    // StreamProcessor data input has 8x128 bit FIFO, to allow a few instructions to operate on the same vector without stalling the data flow.
    //
    // Trigger position is a function of the host (because there is no deep memory in FPGA). Host will use sample index tag FIFO side-channel to determine
    // what to take out of a circular buffer, allowing for a healthy amount of pre-trigger data.
    //
    // A small amount of instruction memory (32 instructions?) gets updated quickly at runtime when needed. New program triggers StreamProcessor reset. Automatic wrapparound of program counter.
    //

    public enum StreamProcessorRegister : byte
    {
        HoldSampleCounter,  // Instruction will hold until counter = 0 (doing a partial vector operation if appropriate). Maybe 36 bits on FPGA to allow for 60 seconds at 1GSPS?
        ResetSampleCounter, // Instruction will stop operating when counter = 0 and do a reset back to beginning of program (doing a partial vector operation if appropriate). Maybe 36 bits on FPGA to allow for 60 seconds at 1GSPS?
        InterleaveCount,    // 0 = off, 2 = 2 channels, 4 = 4 channels.
        InterleaveIndex     // 2 channels = 0/1, 4 channels = 0/1/2/3.
    }

    public enum StreamProcessorOperation : byte
    {
        // General operations (no load from input FIFO so use carefully to avoid overflow of input FIFO)
        LoadLiteral,
        ReadGpio,
        WriteGpio,

        // Vector load & process operations (load from input FIFO)
        V128_U8x16_GT,
        V128_U8x16_GTE,
        V128_U8x16_LT,
        V128_U8x16_LTE,
        V128_U16x8_GT,
        V128_U16x8_GTE,
        V128_U16x8_LT,
        V128_U16x8_LTE,
    }

    public interface IStreamProcessorOperands { }

    public struct StreamProcessorInstruction
    {
        public StreamProcessorOperation Operation;
        public IStreamProcessorOperands Operands;
    }

    /// <summary>
    /// Load literal value into register. Literal comes from host as U64, gets co-erced into register width.
    /// </summary>
    public struct LoadLiteral : IStreamProcessorOperands
    {
        public StreamProcessorRegister Destination;
        public ulong Value;
    }

    /// <summary>
    /// V128: 128 bit vector<br />
    /// U8x16: 16x U8 samples (to fit full vector width)<br />
    /// GT: Greater Than comparison<br />
    /// <br />
    /// Scan through vector until first instance of successful comparison, optionally sending sample index tag into host FIFO.<br />
    /// Interleaving support can be enabled with "InterleaveCount" register, to allow for 2 and 4 channel modes from ADC.<br />
    /// Uses register "HoldSampleCounter".<br />
    /// Optionally uses register "ResetSampleCounter", to stop the comparison at a certain index and reset back to beginning of program.
    /// </summary>
    public struct V128_U8x16_GT : IStreamProcessorOperands
    {
        public byte Value;                  // Value to GT compare against. Unit: ADC count.
        public bool SendOutputTag;          // Optionally push successful comparison sample index tag into host FIFO.
        public bool UseResetSampleCounter;  // Used to determine finite runtime for instruction. May be normalised out later into register.
    }

    public class StreamProcessorEmulator
    {
        public StreamProcessorEmulator(List<StreamProcessorInstruction> program)
        {

        }
    }
}
