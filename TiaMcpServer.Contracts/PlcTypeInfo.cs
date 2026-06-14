namespace TiaMcpServer.Contracts;

public class PlcTypeInfo
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Deterministic path, e.g. "PLC/Types/Folder/Type".</summary>
    public string Path { get; set; } = string.Empty;
}
