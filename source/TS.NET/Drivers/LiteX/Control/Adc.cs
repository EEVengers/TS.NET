namespace TS.NET.Driver.LiteX.Control
{
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
            device.WriteL((uint)CSR.CSR_ADC_TRIGGER_CONTROL_ADDR, (uint)(enable ? 1 : 0));
        }
    }
}
