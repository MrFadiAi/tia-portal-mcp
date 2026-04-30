namespace TiaMcpServer.Contracts;

public class WorkerRequest
{
    public string Method { get; set; } = string.Empty;

    public string? ProjectPath { get; set; }

    public string? BlockPath { get; set; }

    public string? YamlContent { get; set; }

    public string? PlcName { get; set; }

    public string? CrossReferenceFilter { get; set; }

    public bool AllowTiaConfirmations { get; set; }
}
