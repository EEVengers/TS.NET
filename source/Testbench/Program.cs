// ============================================================================
// Testbench to get Disasmo output with 'Run' enabled

//using TS.NET;

//Span<sbyte> input = new sbyte[8388608];
//Span<int> windowEndIndices = new int[100];

//var code = new BurstTriggerI8(new BurstTriggerParameters(WindowHighLevel: 20, WindowLowLevel: -20, MinimumInRangePeriod: 1000));

//for (int i = 0; i < 1000;   i++)
//{
//    code.Process(input, windowEndIndices, out int windowEndCount);
//}

// ============================================================================
// Testbench to test LiteX functionality

using Microsoft.Extensions.Logging;
using TS.NET.Driver.LiteX;
using TS.NET.Driver.LiteX.Board;
using TS.NET.Driver.Shared;

var loggerFactory = LoggerFactory.Create(configure =>
{
    configure
        .ClearProviders()
        .AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });
});

var thunderscopes = Thunderscopes.List("LiteX");
if (thunderscopes.Count == 0)
    return;

// SPI devices:
//    LMH6518 Ch1 - CS 0
//    LMH6518 Ch2 - CS 1
//    LMH6518 Ch3 - CS 2
//    LMH6518 Ch4 - CS 3
//    HMCAD1520 - CS 4
// I2C devices:
//    ZL30260 (PLL) - 0x74
//    MCP4728 (trim offset DAC) - 0x60
//    MCP443x (trim sensitivity DAC) - 0x2C
// GPIO:
//    Termination Ch1/Ch2/Ch3/Ch4
//    Attenuator Ch1/Ch2/Ch3/Ch4
//    Coupling Ch1/Ch2/Ch3/Ch4

var litePcie = new LitePcie();
litePcie.Open(thunderscopes[0].DevicePath);

var gpio = new GpioBit(loggerFactory, litePcie, Constants.Afe_Attenuator_Register, Constants.Afe_Attenuator_Mask_Ch1);

for(int i = 0; i < 20; i++)
{
    gpio.Put(true);
    Thread.Sleep(500);
    gpio.Put(false);
    Thread.Sleep(500);
}

//Console.WriteLine(gpio.Get());
//gpio.Put(true);
//Console.WriteLine(gpio.Get());
//gpio.Put(false);
//Console.WriteLine(gpio.Get());

//var spiBus = new LiteSpiBus(loggerFactory, litePcie, CSR.CSR_MAIN_SPI_BASE, CSR.CSR_MAIN_SPI_CS_SEL_SIZE);
//var spiDevice = new LiteSpiDevice(loggerFactory, spiBus, Constants.CS_LMH6518_Ch1);
//spiDevice.Write();

var i2cBus = new LiteI2cBus(loggerFactory, litePcie);
var i2cDevice = new LiteI2cDevice(loggerFactory, i2cBus, Constants.I2C_MCP4728);

litePcie.Close();

Console.WriteLine("End");