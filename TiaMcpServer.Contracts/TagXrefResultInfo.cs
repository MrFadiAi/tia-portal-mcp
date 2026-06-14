using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

public class TagXrefReferenceInfo
{
    public string PlcName { get; set; } = string.Empty;

    /// <summary>The block that references the tag.</summary>
    public string Block { get; set; } = string.Empty;

    public string BlockType { get; set; } = string.Empty;

    /// <summary>Authoritative access from the compiled cross-reference: Read / Write / ReadWrite.</summary>
    public string Access { get; set; } = string.Empty;

    public string ReferenceType { get; set; } = string.Empty;

    /// <summary>Network/section within the block.</summary>
    public string Location { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;
}

public class TagXrefResultInfo
{
    public string Tag { get; set; } = string.Empty;

    public string? PlcName { get; set; }

    public string Address { get; set; } = string.Empty;

    public bool Found { get; set; }

    public int ReferenceCount { get; set; }

    public List<TagXrefReferenceInfo> References { get; set; } = new();

    public List<string> Messages { get; set; } = new();
}
