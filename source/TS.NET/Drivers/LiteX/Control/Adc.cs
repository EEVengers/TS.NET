namespace TS.NET.Driver.LiteX.Control
{
    internal class LiteXAdcRegisters
    {
        public const long CSR_BASE = 0x0L;

        public const long CSR_ADC_BASE = CSR_BASE + 0x0L;
        public const long CSR_ADC_CONTROL_ADDR = CSR_BASE + 0x0L;
        public const int CSR_ADC_CONTROL_SIZE = 1;
        public const long CSR_ADC_TRIGGER_CONTROL_ADDR = CSR_BASE + 0x4L;
        public const int CSR_ADC_TRIGGER_CONTROL_SIZE = 1;
        public const long CSR_ADC_HAD1511_CONTROL_ADDR = CSR_BASE + 0x8L;
        public const int CSR_ADC_HAD1511_CONTROL_SIZE = 1;
        public const long CSR_ADC_HAD1511_STATUS_ADDR = CSR_BASE + 0xCL;
        public const int CSR_ADC_HAD1511_STATUS_SIZE = 1;
        public const long CSR_ADC_HAD1511_DOWNSAMPLING_ADDR = CSR_BASE + 0x10L;
        public const int CSR_ADC_HAD1511_DOWNSAMPLING_SIZE = 1;
        public const long CSR_ADC_HAD1511_RANGE_ADDR = CSR_BASE + 0x14L;
        public const int CSR_ADC_HAD1511_RANGE_SIZE = 1;
        public const long CSR_ADC_HAD1511_BITSLIP_COUNT_ADDR = CSR_BASE + 0x18L;
        public const int CSR_ADC_HAD1511_BITSLIP_COUNT_SIZE = 1;
        public const long CSR_ADC_HAD1511_SAMPLE_COUNT_ADDR = CSR_BASE + 0x1cL;
        public const int CSR_ADC_HAD1511_SAMPLE_COUNT_SIZE = 1;
        public const long CSR_ADC_HAD1511_DATA_CHANNELS_ADDR = CSR_BASE + 0x20L;
        public const int CSR_ADC_HAD1511_DATA_CHANNELS_SIZE = 1;
    }

    internal class Adc
    {
        private readonly LitePcie device;

        public Adc(LitePcie device)
        {
            this.device = device;
        }

        public void Init()
        {

        }

        public void Run(bool enable)
        {
            device.WriteL((uint)LiteXAdcRegisters.CSR_ADC_TRIGGER_CONTROL_ADDR, (uint)(enable ? 1 : 0));
        }
    }
}
