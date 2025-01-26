using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Testbench")]
namespace TS.NET.Driver.LiteX
{
    internal class CSR
    {
        internal const ulong CSR_BASE = 0x0;

        //--------------------------------------------------------------------------------
        // CSR Registers/Fields Definition.
        //--------------------------------------------------------------------------------

        /* DNA Registers */
        internal const ulong CSR_DNA_BASE = (CSR_BASE + 0x0);
        internal const ulong CSR_DNA_ID_ADDR = (CSR_BASE + 0x0);
        internal const int CSR_DNA_ID_SIZE = 2;

        /* DNA Fields */

        /* IDENTIFIER_MEM Registers */
        internal const ulong CSR_IDENTIFIER_MEM_BASE = (CSR_BASE + 0x800);

        /* IDENTIFIER_MEM Fields */

        /* PCIE_PHY Registers */
        internal const ulong CSR_PCIE_PHY_BASE = (CSR_BASE + 0x1000);
        internal const ulong CSR_PCIE_PHY_PHY_LINK_STATUS_ADDR = (CSR_BASE + 0x1000);
        internal const int CSR_PCIE_PHY_PHY_LINK_STATUS_SIZE = 1;
        internal const ulong CSR_PCIE_PHY_PHY_MSI_ENABLE_ADDR = (CSR_BASE + 0x1004);
        internal const int CSR_PCIE_PHY_PHY_MSI_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_PHY_PHY_MSIX_ENABLE_ADDR = (CSR_BASE + 0x1008);
        internal const int CSR_PCIE_PHY_PHY_MSIX_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_PHY_PHY_BUS_MASTER_ENABLE_ADDR = (CSR_BASE + 0x100c);
        internal const int CSR_PCIE_PHY_PHY_BUS_MASTER_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_PHY_PHY_MAX_REQUEST_SIZE_ADDR = (CSR_BASE + 0x1010);
        internal const int CSR_PCIE_PHY_PHY_MAX_REQUEST_SIZE_SIZE = 1;
        internal const ulong CSR_PCIE_PHY_PHY_MAX_PAYLOAD_SIZE_ADDR = (CSR_BASE + 0x1014);
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

        /* PCIE_MSI Registers */
        internal const ulong CSR_PCIE_MSI_BASE = (CSR_BASE + 0x1800);
        internal const ulong CSR_PCIE_MSI_ENABLE_ADDR = (CSR_BASE + 0x1800);
        internal const int CSR_PCIE_MSI_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_MSI_CLEAR_ADDR = (CSR_BASE + 0x1804);
        internal const int CSR_PCIE_MSI_CLEAR_SIZE = 1;
        internal const ulong CSR_PCIE_MSI_VECTOR_ADDR = (CSR_BASE + 0x1808);
        internal const int CSR_PCIE_MSI_VECTOR_SIZE = 1;

        /* PCIE_MSI Fields */

        /* PCIE_ENDPOINT Registers */
        internal const ulong CSR_PCIE_ENDPOINT_BASE = (CSR_BASE + 0x2000);
        internal const ulong CSR_PCIE_ENDPOINT_PHY_LINK_STATUS_ADDR = (CSR_BASE + 0x2000);
        internal const int CSR_PCIE_ENDPOINT_PHY_LINK_STATUS_SIZE = 1;
        internal const ulong CSR_PCIE_ENDPOINT_PHY_MSI_ENABLE_ADDR = (CSR_BASE + 0x2004);
        internal const int CSR_PCIE_ENDPOINT_PHY_MSI_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_ENDPOINT_PHY_MSIX_ENABLE_ADDR = (CSR_BASE + 0x2008);
        internal const int CSR_PCIE_ENDPOINT_PHY_MSIX_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_ENDPOINT_PHY_BUS_MASTER_ENABLE_ADDR = (CSR_BASE + 0x200c);
        internal const int CSR_PCIE_ENDPOINT_PHY_BUS_MASTER_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_ENDPOINT_PHY_MAX_REQUEST_SIZE_ADDR = (CSR_BASE + 0x2010);
        internal const int CSR_PCIE_ENDPOINT_PHY_MAX_REQUEST_SIZE_SIZE = 1;
        internal const ulong CSR_PCIE_ENDPOINT_PHY_MAX_PAYLOAD_SIZE_ADDR = (CSR_BASE + 0x2014);
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

        /* PCIE_DMA0 Registers */
        internal const ulong CSR_PCIE_DMA0_BASE = (CSR_BASE + 0x2800);
        internal const ulong CSR_PCIE_DMA0_WRITER_ENABLE_ADDR = (CSR_BASE + 0x2800);
        internal const int CSR_PCIE_DMA0_WRITER_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_WRITER_TABLE_VALUE_ADDR = (CSR_BASE + 0x2804);
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_VALUE_SIZE = 2;
        internal const ulong CSR_PCIE_DMA0_WRITER_TABLE_WE_ADDR = (CSR_BASE + 0x280c);
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_WE_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_WRITER_TABLE_LOOP_PROG_N_ADDR = (CSR_BASE + 0x2810);
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_LOOP_PROG_N_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_WRITER_TABLE_LOOP_STATUS_ADDR = (CSR_BASE + 0x2814);
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_LOOP_STATUS_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_WRITER_TABLE_LEVEL_ADDR = (CSR_BASE + 0x2818);
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_LEVEL_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_WRITER_TABLE_RESET_ADDR = (CSR_BASE + 0x281c);
        internal const int CSR_PCIE_DMA0_WRITER_TABLE_RESET_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_READER_ENABLE_ADDR = (CSR_BASE + 0x2820);
        internal const int CSR_PCIE_DMA0_READER_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_READER_TABLE_VALUE_ADDR = (CSR_BASE + 0x2824);
        internal const int CSR_PCIE_DMA0_READER_TABLE_VALUE_SIZE = 2;
        internal const ulong CSR_PCIE_DMA0_READER_TABLE_WE_ADDR = (CSR_BASE + 0x282c);
        internal const int CSR_PCIE_DMA0_READER_TABLE_WE_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_READER_TABLE_LOOP_PROG_N_ADDR = (CSR_BASE + 0x2830);
        internal const int CSR_PCIE_DMA0_READER_TABLE_LOOP_PROG_N_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_READER_TABLE_LOOP_STATUS_ADDR = (CSR_BASE + 0x2834);
        internal const int CSR_PCIE_DMA0_READER_TABLE_LOOP_STATUS_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_READER_TABLE_LEVEL_ADDR = (CSR_BASE + 0x2838);
        internal const int CSR_PCIE_DMA0_READER_TABLE_LEVEL_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_READER_TABLE_RESET_ADDR = (CSR_BASE + 0x283c);
        internal const int CSR_PCIE_DMA0_READER_TABLE_RESET_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_LOOPBACK_ENABLE_ADDR = (CSR_BASE + 0x2840);
        internal const int CSR_PCIE_DMA0_LOOPBACK_ENABLE_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_BUFFERING_READER_FIFO_CONTROL_ADDR = (CSR_BASE + 0x2844);
        internal const int CSR_PCIE_DMA0_BUFFERING_READER_FIFO_CONTROL_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_BUFFERING_READER_FIFO_STATUS_ADDR = (CSR_BASE + 0x2848);
        internal const int CSR_PCIE_DMA0_BUFFERING_READER_FIFO_STATUS_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_BUFFERING_WRITER_FIFO_CONTROL_ADDR = (CSR_BASE + 0x284c);
        internal const int CSR_PCIE_DMA0_BUFFERING_WRITER_FIFO_CONTROL_SIZE = 1;
        internal const ulong CSR_PCIE_DMA0_BUFFERING_WRITER_FIFO_STATUS_ADDR = (CSR_BASE + 0x2850);
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

        /* CTRL Registers */
        internal const ulong CSR_CTRL_BASE = (CSR_BASE + 0x3000);
        internal const ulong CSR_CTRL_RESET_ADDR = (CSR_BASE + 0x3000);
        internal const int CSR_CTRL_RESET_SIZE = 1;
        internal const ulong CSR_CTRL_SCRATCH_ADDR = (CSR_BASE + 0x3004);
        internal const int CSR_CTRL_SCRATCH_SIZE = 1;
        internal const ulong CSR_CTRL_BUS_ERRORS_ADDR = (CSR_BASE + 0x3008);
        internal const int CSR_CTRL_BUS_ERRORS_SIZE = 1;

        /* CTRL Fields */
        internal const int CSR_CTRL_RESET_SOC_RST_OFFSET = 0;
        internal const int CSR_CTRL_RESET_SOC_RST_SIZE = 1;
        internal const int CSR_CTRL_RESET_CPU_RST_OFFSET = 1;
        internal const int CSR_CTRL_RESET_CPU_RST_SIZE = 1;

        /* SPIFLASH_CORE Registers */
        internal const ulong CSR_SPIFLASH_CORE_BASE = (CSR_BASE + 0x3800);
        internal const ulong CSR_SPIFLASH_CORE_MMAP_DUMMY_BITS_ADDR = (CSR_BASE + 0x3800);
        internal const int CSR_SPIFLASH_CORE_MMAP_DUMMY_BITS_SIZE = 1;
        internal const ulong CSR_SPIFLASH_CORE_MMAP_WRITE_CONFIG_ADDR = (CSR_BASE + 0x3804);
        internal const int CSR_SPIFLASH_CORE_MMAP_WRITE_CONFIG_SIZE = 1;
        internal const ulong CSR_SPIFLASH_CORE_MASTER_CS_ADDR = (CSR_BASE + 0x3808);
        internal const int CSR_SPIFLASH_CORE_MASTER_CS_SIZE = 1;
        internal const ulong CSR_SPIFLASH_CORE_MASTER_PHYCONFIG_ADDR = (CSR_BASE + 0x380c);
        internal const int CSR_SPIFLASH_CORE_MASTER_PHYCONFIG_SIZE = 1;
        internal const ulong CSR_SPIFLASH_CORE_MASTER_RXTX_ADDR = (CSR_BASE + 0x3810);
        internal const int CSR_SPIFLASH_CORE_MASTER_RXTX_SIZE = 1;
        internal const ulong CSR_SPIFLASH_CORE_MASTER_STATUS_ADDR = (CSR_BASE + 0x3814);
        internal const int CSR_SPIFLASH_CORE_MASTER_STATUS_SIZE = 1;

        /* SPIFLASH_CORE Fields */
        internal const int CSR_SPIFLASH_CORE_MMAP_WRITE_CONFIG_WRITE_ENABLE_OFFSET = 0;
        internal const int CSR_SPIFLASH_CORE_MMAP_WRITE_CONFIG_WRITE_ENABLE_SIZE = 1;
        internal const int CSR_SPIFLASH_CORE_MASTER_PHYCONFIG_LEN_OFFSET = 0;
        internal const int CSR_SPIFLASH_CORE_MASTER_PHYCONFIG_LEN_SIZE = 8;
        internal const int CSR_SPIFLASH_CORE_MASTER_PHYCONFIG_WIDTH_OFFSET = 8;
        internal const int CSR_SPIFLASH_CORE_MASTER_PHYCONFIG_WIDTH_SIZE = 4;
        internal const int CSR_SPIFLASH_CORE_MASTER_PHYCONFIG_MASK_OFFSET = 16;
        internal const int CSR_SPIFLASH_CORE_MASTER_PHYCONFIG_MASK_SIZE = 8;
        internal const int CSR_SPIFLASH_CORE_MASTER_STATUS_TX_READY_OFFSET = 0;
        internal const int CSR_SPIFLASH_CORE_MASTER_STATUS_TX_READY_SIZE = 1;
        internal const int CSR_SPIFLASH_CORE_MASTER_STATUS_RX_READY_OFFSET = 1;
        internal const int CSR_SPIFLASH_CORE_MASTER_STATUS_RX_READY_SIZE = 1;

        /* SPIFLASH_PHY Registers */
        internal const ulong CSR_SPIFLASH_PHY_BASE = (CSR_BASE + 0x4000);
        internal const ulong CSR_SPIFLASH_PHY_CLK_DIVISOR_ADDR = (CSR_BASE + 0x4000);
        internal const int CSR_SPIFLASH_PHY_CLK_DIVISOR_SIZE = 1;

        /* SPIFLASH_PHY Fields */

        /* FLASH_ADAPTER Registers */
        internal const ulong CSR_FLASH_ADAPTER_BASE = (CSR_BASE + 0x4800);
        internal const ulong CSR_FLASH_ADAPTER_WINDOW0_ADDR = (CSR_BASE + 0x4800);
        internal const int CSR_FLASH_ADAPTER_WINDOW0_SIZE = 1;

        /* FLASH_ADAPTER Fields */

        /* ICAP Registers */
        internal const ulong CSR_ICAP_BASE = (CSR_BASE + 0x5000);
        internal const ulong CSR_ICAP_ADDR_ADDR = (CSR_BASE + 0x5000);
        internal const int CSR_ICAP_ADDR_SIZE = 1;
        internal const ulong CSR_ICAP_DATA_ADDR = (CSR_BASE + 0x5004);
        internal const int CSR_ICAP_DATA_SIZE = 1;
        internal const ulong CSR_ICAP_WRITE_ADDR = (CSR_BASE + 0x5008);
        internal const int CSR_ICAP_WRITE_SIZE = 1;
        internal const ulong CSR_ICAP_DONE_ADDR = (CSR_BASE + 0x500c);
        internal const int CSR_ICAP_DONE_SIZE = 1;
        internal const ulong CSR_ICAP_READ_ADDR = (CSR_BASE + 0x5010);
        internal const int CSR_ICAP_READ_SIZE = 1;

        /* ICAP Fields */

        /* XADC Registers */
        internal const ulong CSR_XADC_BASE = (CSR_BASE + 0x5800);
        internal const ulong CSR_XADC_TEMPERATURE_ADDR = (CSR_BASE + 0x5800);
        internal const int CSR_XADC_TEMPERATURE_SIZE = 1;
        internal const ulong CSR_XADC_VCCINT_ADDR = (CSR_BASE + 0x5804);
        internal const int CSR_XADC_VCCINT_SIZE = 1;
        internal const ulong CSR_XADC_VCCAUX_ADDR = (CSR_BASE + 0x5808);
        internal const int CSR_XADC_VCCAUX_SIZE = 1;
        internal const ulong CSR_XADC_VCCBRAM_ADDR = (CSR_BASE + 0x580c);
        internal const int CSR_XADC_VCCBRAM_SIZE = 1;
        internal const ulong CSR_XADC_EOC_ADDR = (CSR_BASE + 0x5810);
        internal const int CSR_XADC_EOC_SIZE = 1;
        internal const ulong CSR_XADC_EOS_ADDR = (CSR_BASE + 0x5814);
        internal const int CSR_XADC_EOS_SIZE = 1;

        /* XADC Fields */

        /* ADC Registers */
        internal const ulong CSR_ADC_BASE = (CSR_BASE + 0x6000);
        internal const ulong CSR_ADC_CONTROL_ADDR = (CSR_BASE + 0x6000);
        internal const int CSR_ADC_CONTROL_SIZE = 1;
        internal const ulong CSR_ADC_TRIGGER_CONTROL_ADDR = (CSR_BASE + 0x6004);
        internal const int CSR_ADC_TRIGGER_CONTROL_SIZE = 1;
        internal const ulong CSR_ADC_HAD1511_CONTROL_ADDR = (CSR_BASE + 0x6008);
        internal const int CSR_ADC_HAD1511_CONTROL_SIZE = 1;
        internal const ulong CSR_ADC_HAD1511_STATUS_ADDR = (CSR_BASE + 0x600c);
        internal const int CSR_ADC_HAD1511_STATUS_SIZE = 1;
        internal const ulong CSR_ADC_HAD1511_DOWNSAMPLING_ADDR = (CSR_BASE + 0x6010);
        internal const int CSR_ADC_HAD1511_DOWNSAMPLING_SIZE = 1;
        internal const ulong CSR_ADC_HAD1511_RANGE_ADDR = (CSR_BASE + 0x6014);
        internal const int CSR_ADC_HAD1511_RANGE_SIZE = 1;
        internal const ulong CSR_ADC_HAD1511_BITSLIP_COUNT_ADDR = (CSR_BASE + 0x6018);
        internal const int CSR_ADC_HAD1511_BITSLIP_COUNT_SIZE = 1;
        internal const ulong CSR_ADC_HAD1511_SAMPLE_COUNT_ADDR = (CSR_BASE + 0x601c);
        internal const int CSR_ADC_HAD1511_SAMPLE_COUNT_SIZE = 1;
        internal const ulong CSR_ADC_HAD1511_DATA_CHANNELS_ADDR = (CSR_BASE + 0x6020);
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

        /* FRONTEND Registers */
        internal const ulong CSR_FRONTEND_BASE = (CSR_BASE + 0x6800);
        internal const ulong CSR_FRONTEND_CONTROL_ADDR = (CSR_BASE + 0x6800);
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
        internal const ulong CSR_I2C_BASE = (CSR_BASE + 0x7000);
        internal const ulong CSR_I2C_PHY_SPEED_MODE_ADDR = (CSR_BASE + 0x7000);
        internal const int CSR_I2C_PHY_SPEED_MODE_SIZE = 1;
        internal const ulong CSR_I2C_MASTER_ACTIVE_ADDR = (CSR_BASE + 0x7004);
        internal const int CSR_I2C_MASTER_ACTIVE_SIZE = 1;
        internal const ulong CSR_I2C_MASTER_SETTINGS_ADDR = (CSR_BASE + 0x7008);
        internal const int CSR_I2C_MASTER_SETTINGS_SIZE = 1;
        internal const ulong CSR_I2C_MASTER_ADDR_ADDR = (CSR_BASE + 0x700c);
        internal const int CSR_I2C_MASTER_ADDR_SIZE = 1;
        internal const ulong CSR_I2C_MASTER_RXTX_ADDR = (CSR_BASE + 0x7010);
        internal const int CSR_I2C_MASTER_RXTX_SIZE = 1;
        internal const ulong CSR_I2C_MASTER_STATUS_ADDR = (CSR_BASE + 0x7014);
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

        /* LEDS Registers */
        internal const ulong CSR_LEDS_BASE = (CSR_BASE + 0x7800);
        internal const ulong CSR_LEDS_OUT_ADDR = (CSR_BASE + 0x7800);
        internal const int CSR_LEDS_OUT_SIZE = 1;
        internal const ulong CSR_LEDS_PWM_ENABLE_ADDR = (CSR_BASE + 0x7804);
        internal const int CSR_LEDS_PWM_ENABLE_SIZE = 1;
        internal const ulong CSR_LEDS_PWM_WIDTH_ADDR = (CSR_BASE + 0x7808);
        internal const int CSR_LEDS_PWM_WIDTH_SIZE = 1;
        internal const ulong CSR_LEDS_PWM_PERIOD_ADDR = (CSR_BASE + 0x780c);
        internal const int CSR_LEDS_PWM_PERIOD_SIZE = 1;

        /* LEDS Fields */

        /* MAIN_SPI Registers */
        internal const ulong CSR_MAIN_SPI_BASE = (CSR_BASE + 0x8000);
        internal const ulong CSR_MAIN_SPI_CONTROL_ADDR = (CSR_BASE + 0x8000);
        internal const int CSR_MAIN_SPI_CONTROL_SIZE = 1;
        internal const ulong CSR_MAIN_SPI_STATUS_ADDR = (CSR_BASE + 0x8004);
        internal const int CSR_MAIN_SPI_STATUS_SIZE = 1;
        internal const ulong CSR_MAIN_SPI_MOSI_ADDR = (CSR_BASE + 0x8008);
        internal const int CSR_MAIN_SPI_MOSI_SIZE = 1;
        internal const ulong CSR_MAIN_SPI_MISO_ADDR = (CSR_BASE + 0x800c);
        internal const int CSR_MAIN_SPI_MISO_SIZE = 1;
        internal const ulong CSR_MAIN_SPI_CS_ADDR = (CSR_BASE + 0x8010);
        internal const int CSR_MAIN_SPI_CS_SIZE = 1;
        internal const ulong CSR_MAIN_SPI_LOOPBACK_ADDR = (CSR_BASE + 0x8014);
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

        /* PROBE_COMPENSATION Registers */
        internal const ulong CSR_PROBE_COMPENSATION_BASE = (CSR_BASE + 0x8800);
        internal const ulong CSR_PROBE_COMPENSATION_ENABLE_ADDR = (CSR_BASE + 0x8800);
        internal const int CSR_PROBE_COMPENSATION_ENABLE_SIZE = 1;
        internal const ulong CSR_PROBE_COMPENSATION_WIDTH_ADDR = (CSR_BASE + 0x8804);
        internal const int CSR_PROBE_COMPENSATION_WIDTH_SIZE = 1;
        internal const ulong CSR_PROBE_COMPENSATION_PERIOD_ADDR = (CSR_BASE + 0x8808);
        internal const int CSR_PROBE_COMPENSATION_PERIOD_SIZE = 1;
    }
}
