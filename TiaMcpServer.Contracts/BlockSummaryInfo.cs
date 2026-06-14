namespace TiaMcpServer.Contracts;

public class BlockSummaryInfo
{
    public string Name { get; set; } = string.Empty;

    /// <summary>FC, FB, OB, GlobalDB, InstanceDB, ArrayDB, etc.</summary>
    public string BlockType { get; set; } = string.Empty;

    public int Number { get; set; }

    /// <summary>STL, SCL (SCL/ST), LAD, FBD, GRAPH, DB, etc.</summary>
    public string ProgrammingLanguage { get; set; } = string.Empty;

    /// <summary>Deterministic path, e.g. "PLC/Blocks/Folder/Block".</summary>
    public string Path { get; set; } = string.Empty;
}
