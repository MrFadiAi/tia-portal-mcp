namespace TiaMcpServer.Contracts;

/// <summary>
/// Result of an <c>update_type_content</c> request. Guards (type exists; declared XML name
/// matches) refuse before anything is written, so a success payload always means Applied.
/// </summary>
public class PlcTypeUpdateResultInfo
{
    /// <summary>Type name the request targeted.</summary>
    public string TypeName { get; set; } = "";

    /// <summary>Type name the XML declared (equals <see cref="TypeName"/> on success — the guard enforced it).</summary>
    public string? DeclaredName { get; set; }

    /// <summary>True = the XML import ran (ImportOptions.Override).</summary>
    public bool Applied { get; set; }

    public string Message { get; set; } = "";
}
