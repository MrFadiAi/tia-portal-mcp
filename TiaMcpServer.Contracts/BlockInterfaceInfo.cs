using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

public class BlockInterfaceInfo
{
    public string BlockName { get; set; } = string.Empty;
    public string BlockType { get; set; } = string.Empty;
    public int BlockNumber { get; set; }
    public string ProgrammingLanguage { get; set; } = string.Empty;
    public List<BlockSectionInfo> Sections { get; set; } = new();
    public string? DiagnosticMessage { get; set; }
}

public class BlockSectionInfo
{
    public string SectionName { get; set; } = string.Empty;
    public List<BlockParameterInfo> Parameters { get; set; } = new();
}

public class BlockParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public string? StartValue { get; set; }
    public string? Comment { get; set; }
    public bool? accessible { get; set; }
    public bool? visible { get; set; }
    public string? Remanence { get; set; }
    public int? Offset { get; set; }
}
