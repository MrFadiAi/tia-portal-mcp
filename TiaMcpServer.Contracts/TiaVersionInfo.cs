namespace TiaMcpServer.Contracts;

public class TiaVersionInfo
{
    public int MajorVersion { get; set; }
    public string DisplayName { get; set; } = "";
    public string? InstallPath { get; set; }
    public string? DllDirectory { get; set; }
    public bool UsesSplitDlls { get; set; }
}
