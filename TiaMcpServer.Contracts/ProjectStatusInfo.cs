namespace TiaMcpServer.Contracts;

public class ProjectStatusInfo
{
    public bool IsOpen { get; set; }

    public string? Name { get; set; }

    public string? Path { get; set; }

    public string? Version { get; set; }

    public string? Author { get; set; }

    public bool? IsModified { get; set; }

    public DateTime? CreationTime { get; set; }

    public DateTime? LastModified { get; set; }

    public string? LastModifiedBy { get; set; }

    public long? Size { get; set; }
}
