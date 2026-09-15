using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

public class HmiTagTraceEntryInfo
{
    public string ScreenName { get; set; } = string.Empty;

    public string ElementName { get; set; } = string.Empty;

    public string ElementType { get; set; } = string.Empty;

    /// <summary>Where the tag came from: "TagBinding" or "Animation".</summary>
    public string Source { get; set; } = string.Empty;

    public string PropertyName { get; set; } = string.Empty;

    public string Tag { get; set; } = string.Empty;

    public int ReferenceCount { get; set; }

    public List<TagReferenceInfo> References { get; set; } = new();
}

public class HmiTagTraceResultInfo
{
    public string? DeviceName { get; set; }

    public string? PlcName { get; set; }

    public int ScreenCount { get; set; }

    public int BindingCount { get; set; }

    public int TracedBlockCount { get; set; }

    public int SkippedProtectedCount { get; set; }

    public List<HmiTagTraceEntryInfo> Traces { get; set; } = new();

    /// <summary>Distinct tags that had no references in block code.</summary>
    public List<string> TagsWithoutReferences { get; set; } = new();

    /// <summary>Named skip list ("NAME (reason)") for blocks the underlying code index
    /// could not read (know-how protected / UDT-inconsistent / unreadable) — discloses
    /// exactly what this result did NOT cover, instead of a bare count.</summary>
    public List<string> SkippedBlocks { get; set; } = new();
}
