using TS.NET.Driver.XMDA;

namespace TS.NET.Driver.LiteX
{
    public class Thunderscope : IThunderscope
    {
        private ThunderscopeCalibration calibration;
        private string revision;
        private bool open = false;
        private nint tsHandle;

        public void Open(uint devIndex, ThunderscopeCalibration calibration, string revision)
        {
            if (open)
                Close();

            this.calibration = calibration;
            this.revision = revision;

            //Initialise();
            tsHandle = Interop.Open(devIndex);
            open = true;
        }

        public void Close()
        {
            if (!open)
                throw new Exception("Thunderscope not open");
            int returnValue = Interop.Close(tsHandle);
            open = false;
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public void Read(ThunderscopeMemory data, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public ThunderscopeChannel GetChannel(int channelIndex)
        {
            throw new NotImplementedException();
        }

        public ThunderscopeHardwareConfig GetConfiguration()
        {
            throw new NotImplementedException();
        }

        public void SetChannel(int channelIndex, ThunderscopeChannel channel)
        {
            throw new NotImplementedException();
        }

        public void SetChannelEnable(int channelIndex, bool enabled)
        {
            throw new NotImplementedException();
        }
    }
}
