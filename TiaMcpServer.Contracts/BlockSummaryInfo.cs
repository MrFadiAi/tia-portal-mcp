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

    /// <summary>False = UDT-inconsistent (a TIA Portal GUI edit leaves this state); the block
    /// cannot be exported until it is compiled again. Read tools auto-compile single blocks;
    /// this flag names the state up front so bulk listings disclose it.</summary>
    public bool IsConsistent { get; set; } = true;

    /// <summary>True = know-how protected; the code body is not readable without the password.</summary>
    public bool IsKnowHowProtected { get; set; }

    /// <summary>True = modified after its last compile (or UDT-inconsistent) — the export
    /// would serve stale/empty content until the block is compiled.</summary>
    public bool IsStale { get; set; }
}
