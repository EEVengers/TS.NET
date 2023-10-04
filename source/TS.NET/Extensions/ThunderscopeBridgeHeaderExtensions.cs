using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace TS.NET
{
    public static class ThunderscopeBridgeHeaderExtensions
    {
        public static ThunderscopeChannel GetTriggerChannel(this ThunderscopeConfiguration configuration, TriggerChannel triggerChannel)
        {
            return triggerChannel switch
            {
                TriggerChannel.One => configuration.Channel1,
                TriggerChannel.Two => configuration.Channel2,
                TriggerChannel.Three => configuration.Channel3,
                TriggerChannel.Four => configuration.Channel4,
                _ => throw new NotImplementedException()
            };
        }

        public static ThunderscopeChannel GetChannel(this ThunderscopeConfiguration configuration, int channelIndex)
        {
            // channelIndex is zero-indexed
            return channelIndex switch
            {
                0 => configuration.Channel1,
                1 => configuration.Channel2,
                2 => configuration.Channel3,
                3 => configuration.Channel4,
                _ => throw new ArgumentException("channel out of range")
            };
        }

        public static void SetChannel(this ref ThunderscopeConfiguration configuration, int channelIndex, ThunderscopeChannel ch)
        {
            // channelIndex is zero-indexed
            switch (channelIndex)
            {
                case 0:
                    configuration.Channel1 = ch;
                    break;
                case 1:
                    configuration.Channel2 = ch;
                    break;
                case 2:
                    configuration.Channel3 = ch;
                    break;
                case 3:
                    configuration.Channel4 = ch;
                    break;
                default:
                    throw new ArgumentException("channel out of range");
            }
        }
    }
}
