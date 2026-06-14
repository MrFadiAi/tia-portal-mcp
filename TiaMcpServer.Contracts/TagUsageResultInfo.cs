using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

public class TagReferenceInfo
{
    public string PlcName { get; set; } = string.Empty;

    public string BlockName { get; set; } = string.Empty;

    public string BlockType { get; set; } = string.Empty;

    public int LineNumber { get; set; }

    public string Line { get; set; } = string.Empty;

    /// <summary>Heuristic access classification: "read", "write", or "unknown".</summary>
    public string Access { get; set; } = "unknown";

    /// <summary>What matched: the tag name, or the absolute address variant.</summary>
    public string? MatchedTerm { get; set; }
}

public class TagUsageResultInfo
{
    public string Tag { get; set; } = string.Empty;

    public string? PlcName { get; set; }

    /// <summary>Logical address(es) resolved for the tag (from the tag tables).</summary>
    public List<string> Addresses { get; set; } = new();

    /// <summary>Address forms searched (English + German STL variants).</summary>
    public List<string> AddressVariantsTried { get; set; } = new();

    public int SearchedBlockCount { get; set; }

    public int SkippedProtectedCount { get; set; }

    public int ReferenceCount { get; set; }

    public List<TagReferenceInfo> References { get; set; } = new();
}
