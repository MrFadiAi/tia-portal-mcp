using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

public class CodeMatchInfo
{
    public string PlcName { get; set; } = string.Empty;

    public string BlockName { get; set; } = string.Empty;

    public string BlockType { get; set; } = string.Empty;

    public string ProgrammingLanguage { get; set; } = string.Empty;

    public int LineNumber { get; set; }

    public string Line { get; set; } = string.Empty;

    public List<string> ContextBefore { get; set; } = new();

    public List<string> ContextAfter { get; set; } = new();
}

public class CodeSearchResultInfo
{
    public string Pattern { get; set; } = string.Empty;

    public string? PlcName { get; set; }

    public int SearchedBlockCount { get; set; }

    public int SkippedProtectedCount { get; set; }

    public int MatchCount { get; set; }

    public List<CodeMatchInfo> Matches { get; set; } = new();
}
