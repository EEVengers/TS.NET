namespace TS.NET.JTAG;

public class D2xxException : Exception
{
    public D2xxException() { }
    public D2xxException(string? message) : base(message) { }
    public D2xxException(string? message, Exception? innerException) : base(message, innerException) { }
}
