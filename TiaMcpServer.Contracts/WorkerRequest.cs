namespace TiaMcpServer.Contracts;

public class WorkerRequest
{
    public string Method { get; set; } = string.Empty;

    public string? ProjectPath { get; set; }

    public string? BlockPath { get; set; }

    public string? YamlContent { get; set; }

    public string? PlcName { get; set; }

    public string? CrossReferenceFilter { get; set; }

    /// <summary>knowhow_unlock: the know-how protection password (provided once by the user, then cached to disk).</summary>
    public string? Password { get; set; }

    public bool AllowTiaConfirmations { get; set; }

    public string? Query { get; set; }

    public string? TypeIdentifier { get; set; }

    public string? DeviceName { get; set; }

    public string? DeviceItemName { get; set; }

    public string? IpAddress { get; set; }

    public string? SubnetMask { get; set; }

    public string? PnDeviceName { get; set; }

    public string? SubnetName { get; set; }

    public string? IoSystemName { get; set; }

    public bool Confirm { get; set; }

    /// <summary>get_block_content: bypass the "already shown" re-read optimization and return the FULL source.
    /// Used by write-tool previews (update/delete block), which need the real current content to compute an
    /// accurate diff and a stable safety hash — the skip-note would make every requested line show as an
    /// addition and hash the note instead of the block.</summary>
    public bool ForceRefresh { get; set; }

    public string? TableName { get; set; }

    public string? FolderPath { get; set; }

    public string? Name { get; set; }

    public string? NewName { get; set; }

    public string? DataType { get; set; }

    public string? LogicalAddress { get; set; }

    public bool? ExternalAccessible { get; set; }

    public bool? ExternalVisible { get; set; }

    public bool? ExternalWritable { get; set; }

    public bool? IsSafety { get; set; }

    public string? Value { get; set; }

    public string? ProjectDirectory { get; set; }

    public string? ProjectName { get; set; }

    public string? Author { get; set; }

    public string? Comment { get; set; }

    public string? TargetDirectory { get; set; }

    public string? TargetName { get; set; }

    public bool ForceRebind { get; set; }

    public bool Rebind { get; set; } = true;

    public string? ArchiveDirectory { get; set; }

    public string? ArchiveName { get; set; }

    public string? ArchiveMode { get; set; }

    public bool SaveBeforeArchive { get; set; } = true;

    public bool SaveBeforeClose { get; set; } = true;

    public string? TypeName { get; set; }

    public int? TiaVersion { get; set; }

    public string? Mode { get; set; }

    public string? ScreenName { get; set; }

    /// <summary>search_code: case-insensitive matching (default true).</summary>
    public bool IgnoreCase { get; set; } = true;

    /// <summary>search_code: lines of context around each match (default 2).</summary>
    public int ContextLines { get; set; } = 2;

    /// <summary>browse_project_tree: max nodes to return per page (pagination). null = whole tree (legacy behavior).</summary>
    public int? MaxNodes { get; set; }

    /// <summary>browse_project_tree: nodes to skip (continuation offset from a previous page's nextSkip). null/0 = start.</summary>
    public int? Skip { get; set; }
}
