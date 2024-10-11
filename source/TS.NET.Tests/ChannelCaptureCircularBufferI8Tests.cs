using System;
using Xunit;

namespace TS.NET.Tests
{
    public class ChannelCaptureCircularBufferI8Tests
    {
        [Fact]
        public void Allocate2GB()
        {
            using var manager = new ChannelCaptureCircularBufferI8(2000000000);
        }

        [Fact]
        public void Testbench()
        {
            //using var captureBuffer = new ChannelCaptureCircularBufferI8(50000000);
            //captureBuffer.Configure(4, 10000000);
            //if(captureBuffer.TryGetWriteCaptureBuffer(out var bufferW1))
            //{
            //    bufferW1.Fill(1);
            //}
            //if (captureBuffer.TryGetWriteCaptureBuffer(out var bufferW2))
            //{
            //    bufferW2.Fill(2);
            //}
            //if (captureBuffer.TryGetWriteCaptureBuffer(out var bufferW3))
            //{
            //    bufferW3.Fill(3);
            //}
            //if (captureBuffer.TryGetWriteCaptureBuffer(out var bufferW4))
            //{
            //    bufferW4.Fill(4);
            //}
            //if(captureBuffer.TryGetReadCaptureBuffer(out var bufferR1))
            //{
            //    Console.WriteLine(bufferR1[0]);
            //}
            //if (captureBuffer.TryGetWriteCaptureBuffer(out var bufferW5))
            //{
            //    bufferW5.Fill(5);
            //}
            //if (captureBuffer.TryGetReadCaptureBuffer(out var bufferR2))
            //{
            //    Console.WriteLine(bufferR2[0]);
            //}
            //if (captureBuffer.TryGetWriteCaptureBuffer(out var bufferW6))
            //{
            //    bufferW6.Fill(6);
            //}
            //if (captureBuffer.TryGetReadCaptureBuffer(out var bufferR3))
            //{
            //    Console.WriteLine(bufferR3[0]);
            //}
            //if (captureBuffer.TryGetReadCaptureBuffer(out var bufferR4))
            //{
            //    Console.WriteLine(bufferR4[0]);
            //}
            //if (captureBuffer.TryGetReadCaptureBuffer(out var bufferR5))
            //{
            //    Console.WriteLine(bufferR5[0]);
            //}
            //if (captureBuffer.TryGetWriteCaptureBuffer(out var bufferW7))
            //{
            //    bufferW7.Fill(7);
            //}
            //if (captureBuffer.TryGetReadCaptureBuffer(out var bufferR6))
            //{
            //    Console.WriteLine(bufferR6[0]);
            //}
            //if (captureBuffer.TryGetReadCaptureBuffer(out var bufferR7))
            //{
            //    Console.WriteLine(bufferR7[0]);
            //}
        }
    }
}
