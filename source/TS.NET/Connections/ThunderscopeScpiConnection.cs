namespace TS.NET;

public class ThunderscopeScpiConnection : TcpScpiConnection
{
    public void Open(string ipAddress)
    {
        Open(ipAddress, 5025);
    }
}
