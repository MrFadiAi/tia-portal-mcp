namespace TiaMcpServer.Contracts;

public class WorkerResponse
{
    public bool Success { get; set; }

    public string? Payload { get; set; }

    public string? Error { get; set; }
}
