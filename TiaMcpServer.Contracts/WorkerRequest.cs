namespace TiaMcpServer.Contracts;

public class WorkerRequest
{
    public string Method { get; set; } = string.Empty;

    public string? ProjectPath { get; set; }
}
