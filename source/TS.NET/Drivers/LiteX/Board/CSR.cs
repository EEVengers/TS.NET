using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("Testbench")]

namespace TS.NET.Driver.LiteX
{
    internal class CSR
    {
        internal const ulong CSR_BASE = 0x0L;

        //--------------------------------------------------------------------------------
        // CSR Registers/Fields Definition.
        //--------------------------------------------------------------------------------

        /* ADC Registers */
        internal const ulong CSR_ADC_BASE = (CSR_BASE + 0x0L);
        internal const ulong CSR_ADC_CONTROL_ADDR = (CSR_BASE + 0x0L);
        internal const int CSR_ADC_CONTROL_SIZE = 1;
        internal const ulong CSR_ADC_TRIGGER_CONTROL_ADDR = (CSR_BASE + 0x4L);
        internal const int CSR_ADC_TRIGGER_CONTROL_SIZE = 1;
        internal const ulong CSR_ADC_HAD1511_CONTROL_ADDR = (CSR_BASE + 0x8L);
        internal const int CSR_ADC_HAD1511_CONTROL_SIZE = 1;
        internal const ulong CSR_ADC_HAD1511_STATUS_ADDR = (CSR_BASE + 0xcL);
        internal const int CSR_ADC_HAD1511_STATUS_SIZE = 1;
        internal const ulong CSR_ADC_HAD1511_DOWNSAMPLING_ADDR = (CSR_BASE + 0x10L);
        internal const int CSR_ADC_HAD1511_DOWNSAMPLING_SIZE = 1;
        internal const ulong CSR_ADC_HAD1511_RANGE_ADDR = (CSR_BASE + 0x14L);
        internal const int CSR_ADC_HAD1511_RANGE_SIZE = 1;
        internal const ulong CSR_ADC_HAD1511_BITSLIP_COUNT_ADDR = (CSR_BASE + 0x18L);
        internal const int CSR_ADC_HAD1511_BITSLIP_COUNT_SIZE = 1;
        internal const ulong CSR_ADC_HAD1511_SAMPLE_COUNT_ADDR = (CSR_BASE + 0x1cL);
        internal const int CSR_ADC_HAD1511_SAMPLE_COUNT_SIZE = 1;
        internal const ulong CSR_ADC_HAD1511_DATA_CHANNELS_ADDR = (CSR_BASE + 0x20L);
        internal const int CSR_ADC_HAD1511_DATA_CHANNELS_SIZE = 1;

        /* ADC Fields */
        internal const int CSR_ADC_CONTROL_ACQ_EN_OFFSET = 0;
        internal const int CSR_ADC_CONTROL_ACQ_EN_SIZE = 1;
        internal const int CSR_ADC_CONTROL_OSC_EN_OFFSET = 1;
        internal const int CSR_ADC_CONTROL_OSC_EN_SIZE = 1;
        internal const int CSR_ADC_CONTROL_RST_OFFSET = 2;
        internal const int CSR_ADC_CONTROL_RST_SIZE = 1;
        internal const int CSR_ADC_CONTROL_PWR_DOWN_OFFSET = 3;
        internal const int CSR_ADC_CONTROL_PWR_DOWN_SIZE = 1;
        internal const int CSR_ADC_TRIGGER_CONTROL_ENABLE_OFFSET = 0;
        internal const int CSR_ADC_TRIGGER_CONTROL_ENABLE_SIZE = 1;
        internal const int CSR_ADC_HAD1511_CONTROL_FRAME_RST_OFFSET = 0;
        internal const int CSR_ADC_HAD1511_CONTROL_FRAME_RST_SIZE = 1;
        internal const int CSR_ADC_HAD1511_CONTROL_DELAY_RST_OFFSET = 1;
        internal const int CSR_ADC_HAD1511_CONTROL_DELAY_RST_SIZE = 1;
        internal const int CSR_ADC_HAD1511_CONTROL_DELAY_INC_OFFSET = 2;
        internal const int CSR_ADC_HAD1511_CONTROL_DELAY_INC_SIZE = 1;
        internal const int CSR_ADC_HAD1511_CONTROL_STAT_RST_OFFSET = 3;
        internal const int CSR_ADC_HAD1511_CONTROL_STAT_RST_SIZE = 1;
        internal const int CSR_ADC_HAD1511_RANGE_MIN01_OFFSET = 0;
        internal const int CSR_ADC_HAD1511_RANGE_MIN01_SIZE = 8;
        internal const int CSR_ADC_HAD1511_RANGE_MAX01_OFFSET = 8;
        internal const int CSR_ADC_HAD1511_RANGE_MAX01_SIZE = 8;
        internal const int CSR_ADC_HAD1511_RANGE_MIN23_OFFSET = 16;
        internal const int CSR_ADC_HAD1511_RANGE_MIN23_SIZE = 8;
        internal const int CSR_ADC_HAD1511_RANGE_MAX23_OFFSET = 24;
        internal const int CSR_ADC_HAD1511_RANGE_MAX23_SIZE = 8;
        internal const int CSR_ADC_HAD1511_DATA_CHANNELS_SHUFFLE_OFFSET = 0;
        internal const int CSR_ADC_HAD1511_DATA_CHANNELS_SHUFFLE_SIZE = 2;
        internal const int CSR_ADC_HAD1511_DATA_CHANNELS_RUN_LENGTH_OFFSET = 2;
        internal const int CSR_ADC_HAD1511_DATA_CHANNELS_RUN_LENGTH_SIZE = 6;

        /* CTRL Registers */
        internal const ulong CSR_CTRL_BASE = (CSR_BASE + 0x800L);
        internal const ulong CSR_CTRL_RESET_ADDR = (CSR_BASE + 0x800L);
        internal const int CSR_CTRL_RESET_SIZE = 1;
        internal const ulong CSR_CTRL_SCRATCH_ADDR = (CSR_BASE + 0x804L);
        internal const int CSR_CTRL_SCRATCH_SIZE = 1;
        internal const ulong CSR_CTRL_BUS_ERRORS_ADDR = (CSR_BASE + 0x808L);
        internal const int CSR_CTRL_BUS_ERRORS_SIZE = 1;

        /* CTRL Fields */
        internal const int CSR_CTRL_RESET_SOC_RST_OFFSET = 0;
        internal const int CSR_CTRL_RESET_SOC_RST_SIZE = 1;
        internal const int CSR_CTRL_RESET_CPU_RST_OFFSET = 1;
        internal const int CSR_CTRL_RESET_CPU_RST_SIZE = 1;

        /* DNA Registers */
        internal const ulong CSR_DNA_BASE = (CSR_BASE + 0x1000L);
        internal const ulong CSR_DNA_ID_ADDR = (CSR_BASE + 0x1000L);
        internal const int CSR_DNA_ID_SIZE = 2;

        /* DNA Fields */

        /* FRONTEND Registers */
        internal const ulong CSR_FRONTEND_BASE = (CSR_BASE + 0x1800L);
        internal const ulong CSR_FRONTEND_CONTROL_ADDR = (CSR_BASE + 0x1800L);
        internal const int CSR_FRONTEND_CONTROL_SIZE = 1;

        /* FRONTEND Fields */
        internal const int CSR_FRONTEND_CONTROL_FE_EN_OFFSET = 0;
        internal const int CSR_FRONTEND_CONTROL_FE_EN_SIZE = 1;
        internal const int CSR_FRONTEND_CONTROL_COUPLING_OFFSET = 8;
        internal const int CSR_FRONTEND_CONTROL_COUPLING_SIZE = 4;
        internal const int CSR_FRONTEND_CONTROL_ATTENUATION_OFFSET = 16;
        internal const int CSR_FRONTEND_CONTROL_ATTENUATION_SIZE = 4;
        internal const int CSR_FRONTEND_CONTROL_TERMINATION_OFFSET = 24;
        internal const int CSR_FRONTEND_CONTROL_TERMINATION_SIZE = 4;

        /* I2C Registers */
        internal const ulong CSR_I2C_BASE = (CSR_BASE + 0x2000L);
        internal const ulong CSR_I2C_PHY_SPEED_MODE_ADDR = (CSR_BASE + 0x2000L);
        internal const int CSR_I2C_PHY_SPEED_MODE_SIZE = 1;
        internal const ulong CSR_I2C_MASTER_ACTIVE_ADDR = (CSR_BASE + 0x2004L);
        internal const int CSR_I2C_MASTER_ACTIVE_SIZE = 1;
        internal const ulong CSR_I2C_MASTER_SETTINGS_ADDR = (CSR_BASE + 0x2008L);
        internal const int CSR_I2C_MASTER_SETTINGS_SIZE = 1;
        internal const ulong CSR_I2C_MASTER_ADDR_ADDR = (CSR_BASE + 0x200cL);
        internal const int CSR_I2C_MASTER_ADDR_SIZE = 1;
        internal const ulong CSR_I2C_MASTER_RXTX_ADDR = (CSR_BASE + 0x2010L);
        internal const int CSR_I2C_MASTER_RXTX_SIZE = 1;
        internal const ulong CSR_I2C_MASTER_STATUS_ADDR = (CSR_BASE + 0x2014L);
        internal const int CSR_I2C_MASTER_STATUS_SIZE = 1;

        /* I2C Fields */
        internal const int CSR_I2C_MASTER_SETTINGS_LEN_TX_OFFSET = 0;
        internal const int CSR_I2C_MASTER_SETTINGS_LEN_TX_SIZE = 3;
        internal const int CSR_I2C_MASTER_SETTINGS_LEN_RX_OFFSET = 8;
        internal const int CSR_I2C_MASTER_SETTINGS_LEN_RX_SIZE = 3;
        internal const int CSR_I2C_MASTER_SETTINGS_RECOVER_OFFSET = 16;
        internal const int CSR_I2C_MASTER_SETTINGS_RECOVER_SIZE = 1;
        internal const int CSR_I2C_MASTER_STATUS_TX_READY_OFFSET = 0;
        internal const int CSR_I2C_MASTER_STATUS_TX_READY_SIZE = 1;
        internal const int CSR_I2C_MASTER_STATUS_RX_READY_OFFSET = 1;
        internal const int CSR_I2C_MASTER_STATUS_RX_READY_SIZE = 1;
        internal const int CSR_I2C_MASTER_STATUS_NACK_OFFSET = 8;
        internal const int CSR_I2C_MASTER_STATUS_NACK_SIZE = 1;
        internal const int CSR_I2C_MASTER_STATUS_TX_UNFINISHED_OFFSET = 16;
        internal const int CSR_I2C_MASTER_STATUS_TX_UNFINISHED_SIZE = 1;
        internal const int CSR_I2C_MASTER_STATUS_RX_UNFINISHED_OFFSET = 17;
        internal const int CSR_I2C_MASTER_STATUS_RX_UNFINISHED_SIZE = 1;

        /* ICAP Registers */
        internal const ulong CSR_ICAP_BASE = (CSR_BASE + 0x2800L);
        internal const ulong CSR_ICAP_ADDR_ADDR = (CSR_BASE + 0x2800L);
        internal const int CSR_ICAP_ADDR_SIZE = 1;
        internal const ulong CSR_ICAP_DATA_ADDR = (CSR_BASE + 0x2804L);
        internal const int CSR_ICAP_DATA_SIZE = 1;
        internal const ulong CSR_ICAP_WRITE_ADDR = (CSR_BASE + 0x2808L);
        internal const int CSR_ICAP_WRITE_SIZE = 1;
        internal const ulong CSR_ICAP_DONE_ADDR = (CSR_BASE + 0x280cL);
        internal const int CSR_ICAP_DONE_SIZE = 1;
        internal const ulong CSR_ICAP_READ_ADDR = (CSR_BASE + 0x2810L);
        internal const int CSR_ICAP_READ_SIZE = 1;

        /* ICAP Fields */

        /* IDENTIFIER_MEM Registers */
        internal const ulong CSR_IDENTIFIER_MEM_BASE = (CSR_BASE + 0x3000L);

        /* IDENTIFIER_MEM Fields */

        /* LEDS Registers */
        internal const ulong CSR_LEDS_BASE = (CSR_BASE + 0x3800L);
        internal const ulong CSR_LEDS_OUT_ADDR = (CSR_BASE + 0x3800L);
        internal const int CSR_LEDS_OUT_SIZE = 1;
        internal const ulong CSR_LEDS_PWM_ENABLE_ADDR = (CSR_BASE + 0x3804L);
        internal const int CSR_LEDS_PWM_ENABLE_SIZE = 1;
        internal const ulong CSR_LEDS_PWM_WIDTH_ADDR = (CSR_BASE + 0x3808L);
        internal const int CSR_LEDS_PWM_WIDTH_SIZE = 1;
        internal const ulong CSR_LEDS_PWM_PERIOD_ADDR = (CSR_BASE + 0x380cL);
        internal const int CSR_LEDS_PWM_PERIOD_SIZE = 1;

        /* LEDS Fields */

        /* MAIN_SPI Registers */
        internal const ulong CSR_MAIN_SPI_BASE = (CSR_BASE + 0x4000L);
        internal const ulong CSR_MAIN_SPI_CONTROL_ADDR = (CSR_BASE + 0x4000L);
        internal const int CSR_MAIN_SPI_CONTROL_SIZE = 1;
        internal const ulong CSR_MAIN_SPI_STATUS_ADDR = (CSR_BASE + 0x4004L);
        internal const int CSR_MAIN_SPI_STATUS_SIZE = 1;
        internal const ulong CSR_MAIN_SPI_MOSI_ADDR = (CSR_BASE + 0x4008L);
        internal const int CSR_MAIN_SPI_MOSI_SIZE = 1;
        internal const ulong CSR_MAIN_SPI_MISO_ADDR = (CSR_BASE + 0x400cL);
        internal const int CSR_MAIN_SPI_MISO_SIZE = 1;
        internal const ulong CSR_MAIN_SPI_CS_ADDR = (CSR_BASE + 0x4010L);
        internal const int CSR_MAIN_SPI_CS_SIZE = 1;
        internal const ulong CSR_MAIN_SPI_LOOPBACK_ADDR = (CSR_BASE + 0x4014L);
        internal const int CSR_MAIN_SPI_LOOPBACK_SIZE = 1;

        /* MAIN_SPI Fields */
        internal const int CSR_MAIN_SPI_CONTROL_START_OFFSET = 0;
        internal const int CSR_MAIN_SPI_CONTROL_START_SIZE = 1;
        internal const int CSR_MAIN_SPI_CONTROL_LENGTH_OFFSET = 8;
        internal const int CSR_MAIN_SPI_CONTROL_LENGTH_SIZE = 8;
        internal const int CSR_MAIN_SPI_STATUS_DONE_OFFSET = 0;
        internal const int CSR_MAIN_SPI_STATUS_DONE_SIZE = 1;
        internal const int CSR_MAIN_SPI_STATUS_MODE_OFFSET = 1;
        internal const int CSR_MAIN_SPI_STATUS_MODE_SIZE = 1;
        internal const int CSR_MAIN_SPI_CS_SEL_OFFSET = 0;
        internal const int CSR_MAIN_SPI_CS_SEL_SIZE = 5;
        internal const int CSR_MAIN_SPI_CS_MODE_OFFSET = 16;
        internal const int CSR_MAIN_SPI_CS_MODE_SIZE = 1;
        internal const int CSR_MAIN_SPI_LOOPBACK_MODE_OFFSET = 0;
        internal const int CSR_MAIN_SPI_LOOPBACK_MODE_SIZE = 1;

        /* PCIE_DMA0 Registers */
        internal const ulong CSR_PCIE_DMA0_BASE = (CSR_BASE + 0x4800L);
        internal const ulong CSR_PCIE_DMA0_WRITER_ENABLE_ADDR = (CSR_BASE + 0x4800L);
        internal const int CSR_PCIE_DMA0_WRITER_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_WRITER_TABLE_VALUE_ADDR = (CSR_BASE + 0x4804L);
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_VALUE_SIZE = 2;
        internal const ulong CSR_PCIE_DMA0_WRITER_TABLE_WE_ADDR = (CSR_BASE + 0x480cL);
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_WE_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_WRITER_TABLE_LOOP_PROG_N_ADDR = (CSR_BASE + 0x4810L);
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_LOOP_PROG_N_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_WRITER_TABLE_LOOP_STATUS_ADDR = (CSR_BASE + 0x4814L);
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_LOOP_STATUS_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_WRITER_TABLE_LEVEL_ADDR = (CSR_BASE + 0x4818L);
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_LEVEL_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_WRITER_TABLE_RESET_ADDR = (CSR_BASE + 0x481cL);
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_RESET_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_READER_ENABLE_ADDR = (CSR_BASE + 0x4820L);
        internal const int CSR_PCIE_DMA0_READER_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_READER_TABLE_VALUE_ADDR = (CSR_BASE + 0x4824L);
        internal const int CSR_PCIE_DMA0_READER_TABLE_VALUE_SIZE = 2;
        internal const ulong CSR_PCIE_DMA0_READER_TABLE_WE_ADDR = (CSR_BASE + 0x482cL);
        internal const int CSR_PCIE_DMA0_READER_TABLE_WE_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_READER_TABLE_LOOP_PROG_N_ADDR = (CSR_BASE + 0x4830L);
        internal const int CSR_PCIE_DMA0_READER_TABLE_LOOP_PROG_N_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_READER_TABLE_LOOP_STATUS_ADDR = (CSR_BASE + 0x4834L);
        internal const int CSR_PCIE_DMA0_READER_TABLE_LOOP_STATUS_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_READER_TABLE_LEVEL_ADDR = (CSR_BASE + 0x4838L);
        internal const int CSR_PCIE_DMA0_READER_TABLE_LEVEL_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_READER_TABLE_RESET_ADDR = (CSR_BASE + 0x483cL);
        internal const int CSR_PCIE_DMA0_READER_TABLE_RESET_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_LOOPBACK_ENABLE_ADDR = (CSR_BASE + 0x4840L);
        internal const int CSR_PCIE_DMA0_LOOPBACK_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_BUFFERING_READER_FIFO_CONTROL_ADDR = (CSR_BASE + 0x4844L);
        internal const int CSR_PCIE_DMA0_BUFFERING_READER_FIFO_CONTROL_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_BUFFERING_READER_FIFO_STATUS_ADDR = (CSR_BASE + 0x4848L);
        internal const int CSR_PCIE_DMA0_BUFFERING_READER_FIFO_STATUS_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_BUFFERING_WRITER_FIFO_CONTROL_ADDR = (CSR_BASE + 0x484cL);
        internal const int CSR_PCIE_DMA0_BUFFERING_WRITER_FIFO_CONTROL_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_BUFFERING_WRITER_FIFO_STATUS_ADDR = (CSR_BASE + 0x4850L);
        internal const int CSR_PCIE_DMA0_BUFFERING_WRITER_FIFO_STATUS_SIZE = 1;

        /* PCIE_DMA0 Fields */
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_VALUE_ADDRESS_LSB_OFFSET = 0;
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_VALUE_ADDRESS_LSB_SIZE = 32;
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_VALUE_LENGTH_OFFSET = 32;
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_VALUE_LENGTH_SIZE = 24;
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_VALUE_IRQ_DISABLE_OFFSET = 56;
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_VALUE_IRQ_DISABLE_SIZE = 1;
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_VALUE_LAST_DISABLE_OFFSET = 57;
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_VALUE_LAST_DISABLE_SIZE = 1;
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_WE_ADDRESS_MSB_OFFSET = 0;
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_WE_ADDRESS_MSB_SIZE = 32;
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_LOOP_STATUS_INDEX_OFFSET = 0;
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_LOOP_STATUS_INDEX_SIZE = 16;
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_LOOP_STATUS_COUNT_OFFSET = 16;
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_LOOP_STATUS_COUNT_SIZE = 16;
        internal const int CSR_PCIE_DMA0_READER_TABLE_VALUE_ADDRESS_LSB_OFFSET = 0;
        internal const int CSR_PCIE_DMA0_READER_TABLE_VALUE_ADDRESS_LSB_SIZE = 32;
        internal const int CSR_PCIE_DMA0_READER_TABLE_VALUE_LENGTH_OFFSET = 32;
        internal const int CSR_PCIE_DMA0_READER_TABLE_VALUE_LENGTH_SIZE = 24;
        internal const int CSR_PCIE_DMA0_READER_TABLE_VALUE_IRQ_DISABLE_OFFSET = 56;
        internal const int CSR_PCIE_DMA0_READER_TABLE_VALUE_IRQ_DISABLE_SIZE = 1;
        internal const int CSR_PCIE_DMA0_READER_TABLE_VALUE_LAST_DISABLE_OFFSET = 57;
        internal const int CSR_PCIE_DMA0_READER_TABLE_VALUE_LAST_DISABLE_SIZE = 1;
        internal const int CSR_PCIE_DMA0_READER_TABLE_WE_ADDRESS_MSB_OFFSET = 0;
        internal const int CSR_PCIE_DMA0_READER_TABLE_WE_ADDRESS_MSB_SIZE = 32;
        internal const int CSR_PCIE_DMA0_READER_TABLE_LOOP_STATUS_INDEX_OFFSET = 0;
        internal const int CSR_PCIE_DMA0_READER_TABLE_LOOP_STATUS_INDEX_SIZE = 16;
        internal const int CSR_PCIE_DMA0_READER_TABLE_LOOP_STATUS_COUNT_OFFSET = 16;
        internal const int CSR_PCIE_DMA0_READER_TABLE_LOOP_STATUS_COUNT_SIZE = 16;
        internal const int CSR_PCIE_DMA0_BUFFERING_READER_FIFO_CONTROL_DEPTH_OFFSET = 0;
        internal const int CSR_PCIE_DMA0_BUFFERING_READER_FIFO_CONTROL_DEPTH_SIZE = 24;
        internal const int CSR_PCIE_DMA0_BUFFERING_READER_FIFO_CONTROL_SCRATCH_OFFSET = 24;
        internal const int CSR_PCIE_DMA0_BUFFERING_READER_FIFO_CONTROL_SCRATCH_SIZE = 4;
        internal const int CSR_PCIE_DMA0_BUFFERING_READER_FIFO_CONTROL_LEVEL_MODE_OFFSET = 31;
        internal const int CSR_PCIE_DMA0_BUFFERING_READER_FIFO_CONTROL_LEVEL_MODE_SIZE = 1;
        internal const int CSR_PCIE_DMA0_BUFFERING_READER_FIFO_STATUS_LEVEL_OFFSET = 0;
        internal const int CSR_PCIE_DMA0_BUFFERING_READER_FIFO_STATUS_LEVEL_SIZE = 24;
        internal const int CSR_PCIE_DMA0_BUFFERING_WRITER_FIFO_CONTROL_DEPTH_OFFSET = 0;
        internal const int CSR_PCIE_DMA0_BUFFERING_WRITER_FIFO_CONTROL_DEPTH_SIZE = 24;
        internal const int CSR_PCIE_DMA0_BUFFERING_WRITER_FIFO_CONTROL_SCRATCH_OFFSET = 24;
        internal const int CSR_PCIE_DMA0_BUFFERING_WRITER_FIFO_CONTROL_SCRATCH_SIZE = 4;
        internal const int CSR_PCIE_DMA0_BUFFERING_WRITER_FIFO_CONTROL_LEVEL_MODE_OFFSET = 31;
        internal const int CSR_PCIE_DMA0_BUFFERING_WRITER_FIFO_CONTROL_LEVEL_MODE_SIZE = 1;
        internal const int CSR_PCIE_DMA0_BUFFERING_WRITER_FIFO_STATUS_LEVEL_OFFSET = 0;
        internal const int CSR_PCIE_DMA0_BUFFERING_WRITER_FIFO_STATUS_LEVEL_SIZE = 24;

        /* PCIE_ENDPOINT Registers */
        internal const ulong CSR_PCIE_ENDPOINT_BASE = (CSR_BASE + 0x5000L);
        internal const ulong CSR_PCIE_ENDPOINT_PHY_LINK_STATUS_ADDR = (CSR_BASE + 0x5000L);
        internal const int CSR_PCIE_ENDPOINT_PHY_LINK_STATUS_SIZE = 1;
        internal const ulong CSR_PCIE_ENDPOINT_PHY_MSI_ENABLE_ADDR = (CSR_BASE + 0x5004L);
        internal const int CSR_PCIE_ENDPOINT_PHY_MSI_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_ENDPOINT_PHY_MSIX_ENABLE_ADDR = (CSR_BASE + 0x5008L);
        internal const int CSR_PCIE_ENDPOINT_PHY_MSIX_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_ENDPOINT_PHY_BUS_MASTER_ENABLE_ADDR = (CSR_BASE + 0x500cL);
        internal const int CSR_PCIE_ENDPOINT_PHY_BUS_MASTER_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_ENDPOINT_PHY_MAX_REQUEST_SIZE_ADDR = (CSR_BASE + 0x5010L);
        internal const int CSR_PCIE_ENDPOINT_PHY_MAX_REQUEST_SIZE_SIZE = 1;
        internal const ulong CSR_PCIE_ENDPOINT_PHY_MAX_PAYLOAD_SIZE_ADDR = (CSR_BASE + 0x5014L);
        internal const int CSR_PCIE_ENDPOINT_PHY_MAX_PAYLOAD_SIZE_SIZE = 1;

        /* PCIE_ENDPOINT Fields */
        internal const int CSR_PCIE_ENDPOINT_PHY_LINK_STATUS_STATUS_OFFSET = 0;
        internal const int CSR_PCIE_ENDPOINT_PHY_LINK_STATUS_STATUS_SIZE = 1;
        internal const int CSR_PCIE_ENDPOINT_PHY_LINK_STATUS_RATE_OFFSET = 1;
        internal const int CSR_PCIE_ENDPOINT_PHY_LINK_STATUS_RATE_SIZE = 1;
        internal const int CSR_PCIE_ENDPOINT_PHY_LINK_STATUS_WIDTH_OFFSET = 2;
        internal const int CSR_PCIE_ENDPOINT_PHY_LINK_STATUS_WIDTH_SIZE = 2;
        internal const int CSR_PCIE_ENDPOINT_PHY_LINK_STATUS_LTSSM_OFFSET = 4;
        internal const int CSR_PCIE_ENDPOINT_PHY_LINK_STATUS_LTSSM_SIZE = 6;

        /* PCIE_MSI Registers */
        internal const ulong CSR_PCIE_MSI_BASE = (CSR_BASE + 0x5800L);
        internal const ulong CSR_PCIE_MSI_ENABLE_ADDR = (CSR_BASE + 0x5800L);
        internal const int CSR_PCIE_MSI_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_MSI_CLEAR_ADDR = (CSR_BASE + 0x5804L);
        internal const int CSR_PCIE_MSI_CLEAR_SIZE = 1;
        internal const ulong CSR_PCIE_MSI_VECTOR_ADDR = (CSR_BASE + 0x5808L);
        internal const int CSR_PCIE_MSI_VECTOR_SIZE = 1;

        /* PCIE_MSI Fields */

        /* PCIE_PHY Registers */
        internal const ulong CSR_PCIE_PHY_BASE = (CSR_BASE + 0x6000L);
        internal const ulong CSR_PCIE_PHY_PHY_LINK_STATUS_ADDR = (CSR_BASE + 0x6000L);
        internal const int CSR_PCIE_PHY_PHY_LINK_STATUS_SIZE = 1;
        internal const ulong CSR_PCIE_PHY_PHY_MSI_ENABLE_ADDR = (CSR_BASE + 0x6004L);
        internal const int CSR_PCIE_PHY_PHY_MSI_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_PHY_PHY_MSIX_ENABLE_ADDR = (CSR_BASE + 0x6008L);
        internal const int CSR_PCIE_PHY_PHY_MSIX_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_PHY_PHY_BUS_MASTER_ENABLE_ADDR = (CSR_BASE + 0x600cL);
        internal const int CSR_PCIE_PHY_PHY_BUS_MASTER_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_PHY_PHY_MAX_REQUEST_SIZE_ADDR = (CSR_BASE + 0x6010L);
        internal const int CSR_PCIE_PHY_PHY_MAX_REQUEST_SIZE_SIZE = 1;
        internal const ulong CSR_PCIE_PHY_PHY_MAX_PAYLOAD_SIZE_ADDR = (CSR_BASE + 0x6014L);
        internal const int CSR_PCIE_PHY_PHY_MAX_PAYLOAD_SIZE_SIZE = 1;

        /* PCIE_PHY Fields */
        internal const int CSR_PCIE_PHY_PHY_LINK_STATUS_STATUS_OFFSET = 0;
        internal const int CSR_PCIE_PHY_PHY_LINK_STATUS_STATUS_SIZE = 1;
        internal const int CSR_PCIE_PHY_PHY_LINK_STATUS_RATE_OFFSET = 1;
        internal const int CSR_PCIE_PHY_PHY_LINK_STATUS_RATE_SIZE = 1;
        internal const int CSR_PCIE_PHY_PHY_LINK_STATUS_WIDTH_OFFSET = 2;
        internal const int CSR_PCIE_PHY_PHY_LINK_STATUS_WIDTH_SIZE = 2;
        internal const int CSR_PCIE_PHY_PHY_LINK_STATUS_LTSSM_OFFSET = 4;
        internal const int CSR_PCIE_PHY_PHY_LINK_STATUS_LTSSM_SIZE = 6;

        /* PROBE_COMPENSATION Registers */
        internal const ulong CSR_PROBE_COMPENSATION_BASE = (CSR_BASE + 0x6800L);
        internal const ulong CSR_PROBE_COMPENSATION_ENABLE_ADDR = (CSR_BASE + 0x6800L);
        internal const int CSR_PROBE_COMPENSATION_ENABLE_SIZE = 1;
        internal const ulong CSR_PROBE_COMPENSATION_WIDTH_ADDR = (CSR_BASE + 0x6804L);
        internal const int CSR_PROBE_COMPENSATION_WIDTH_SIZE = 1;
        internal const ulong CSR_PROBE_COMPENSATION_PERIOD_ADDR = (CSR_BASE + 0x6808L);
        internal const int CSR_PROBE_COMPENSATION_PERIOD_SIZE = 1;

        /* PROBE_COMPENSATION Fields */

        /* XADC Registers */
        internal const ulong CSR_XADC_BASE = (CSR_BASE + 0x7000L);
        internal const ulong CSR_XADC_TEMPERATURE_ADDR = (CSR_BASE + 0x7000L);
        internal const int CSR_XADC_TEMPERATURE_SIZE = 1;
        internal const ulong CSR_XADC_VCCINT_ADDR = (CSR_BASE + 0x7004L);
        internal const int CSR_XADC_VCCINT_SIZE = 1;
        internal const ulong CSR_XADC_VCCAUX_ADDR = (CSR_BASE + 0x7008L);
        internal const int CSR_XADC_VCCAUX_SIZE = 1;
        internal const ulong CSR_XADC_VCCBRAM_ADDR = (CSR_BASE + 0x700cL);
        internal const int CSR_XADC_VCCBRAM_SIZE = 1;
        internal const ulong CSR_XADC_EOC_ADDR = (CSR_BASE + 0x7010L);
        internal const int CSR_XADC_EOC_SIZE = 1;
        internal const ulong CSR_XADC_EOS_ADDR = (CSR_BASE + 0x7014L);
        internal const int CSR_XADC_EOS_SIZE = 1;

        /* XADC Fields */
    }
}
