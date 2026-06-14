namespace TiaMcpServer.Contracts;

public class WorkerResponse
{
    public bool Success { get; set; }

    public string? Payload { get; set; }

    public string? Error { get; set; }

    /// <summary>True when Payload was served from the structural-read cache.</summary>
    public bool Cached { get; set; }
}
