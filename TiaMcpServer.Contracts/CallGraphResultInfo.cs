using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

public class CallGraphNodeInfo
{
    public string Block { get; set; } = string.Empty;

    public string BlockType { get; set; } = string.Empty;

    public string ReferenceType { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;
}

public class CallGraphResultInfo
{
    public string Block { get; set; } = string.Empty;

    public string? PlcName { get; set; }

    public bool Found { get; set; }

    /// <summary>Blocks that reference/call this block.</summary>
    public List<CallGraphNodeInfo> Callers { get; set; } = new();

    /// <summary>Blocks that this block references/calls.</summary>
    public List<CallGraphNodeInfo> Callees { get; set; } = new();

    public List<string> Messages { get; set; } = new();
}
