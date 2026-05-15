namespace TS.NET.JTAG;

internal class BitfileParser
{
    private static readonly byte[] SyncWord = [0xAA, 0x99, 0x55, 0x66];

    public static byte[] ExtractConfigurationStream(byte[] source)
    {
        if (source.Length == 0)
        {
            throw new InvalidOperationException("Bitstream file is empty.");
        }

        var syncOffset = FindSyncWord(source);
        if (syncOffset < 0)
        {
            throw new InvalidOperationException("Could not find Xilinx sync word (0xAA995566) in the supplied file.");
        }

        var result = new byte[source.Length - syncOffset];
        Buffer.BlockCopy(source, syncOffset, result, 0, result.Length);
        return result;
    }

    private static int FindSyncWord(byte[] source)
    {
        for (var i = 0; i <= source.Length - SyncWord.Length; i++)
        {
            var match = true;
            for (var j = 0; j < SyncWord.Length; j++)
            {
                if (source[i + j] != SyncWord[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }
}