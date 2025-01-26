namespace TS.NET.Driver.LiteX
{
    internal static class Constants
    {
        public const int CS_LMH6518_Ch1 = 0;
        public const int CS_LMH6518_Ch2 = 1;
        public const int CS_LMH6518_Ch3 = 2;
        public const int CS_LMH6518_Ch4 = 3;
        public const int CS_HMCAD1520 = 4;

        public const ushort I2C_MCP4728 = 0x60;
        public const ushort I2C_MCP443x = 0x2C;

        public const uint Afe_Attenuator_Register = CSR.CSR_FRONTEND_CONTROL_ADDR;
        public const int Afe_Attenuator_Mask_Ch1 = 1 << (CSR.CSR_FRONTEND_CONTROL_ATTENUATION_OFFSET);
        public const int Afe_Attenuator_Mask_Ch2 = 1 << (CSR.CSR_FRONTEND_CONTROL_ATTENUATION_OFFSET + 1);
        public const int Afe_Attenuator_Mask_Ch3 = 1 << (CSR.CSR_FRONTEND_CONTROL_ATTENUATION_OFFSET + 2);
        public const int Afe_Attenuator_Mask_Ch4 = 1 << (CSR.CSR_FRONTEND_CONTROL_ATTENUATION_OFFSET + 3);
    }
}
