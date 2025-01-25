using Microsoft.Extensions.Logging;

namespace TS.NET.Driver.LiteX.Board
{
    public class GpioBit
    {
        private readonly ILogger logger;
        private readonly LitePcie litePcie;
        private readonly uint gpioRegister;
        private readonly uint bitMask;

        public GpioBit(ILoggerFactory loggerFactory, LitePcie litePcie, uint gpioRegister, uint bitMask)
        {
            logger = loggerFactory.CreateLogger(nameof(GpioBit));
            this.litePcie = litePcie;
            this.gpioRegister = gpioRegister;
            this.bitMask = bitMask;
        }

        public bool Get()
        {
            var registerValue = litePcie.ReadL(gpioRegister);
            return (registerValue & bitMask) > 0;
        }

        public void Put(bool value)
        {
            var registerValue = litePcie.ReadL(gpioRegister);
            if (value)
                litePcie.WriteL(gpioRegister, registerValue | bitMask);
            else
                litePcie.WriteL(gpioRegister, registerValue & ~bitMask);
        }
    }
}
