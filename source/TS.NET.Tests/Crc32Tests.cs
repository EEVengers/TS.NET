using System.Text;
using Xunit;

namespace TS.NET.Tests
{
    public class Crc32Tests
    {
        [Fact]
        public void Crc32_HWID()
        {
            const string json = """{"version":1,"serial":"TS0019","boardRevision":2,"buildConfiguration":"","buildDate":"2025-05-02T05:02:00Z","manufacturingSignature":"abc123"}""";
            var data = Encoding.UTF8.GetBytes(json);

            var crc = ThunderscopeNonVolatileMemory.Crc32(data);

            Assert.Equal([0xF9, 0x15, 0x56, 0x8F], crc);
        }
    }
}
