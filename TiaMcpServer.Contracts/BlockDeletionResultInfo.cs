namespace TiaMcpServer.Contracts;

public class BlockDeletionResultInfo
{
    public string Operation { get; set; } = "delete_block";

    public string BlockPath { get; set; } = string.Empty;

    public string BlockName { get; set; } = string.Empty;

    public int BlockNumber { get; set; }

    public string BlockType { get; set; } = string.Empty;

    public bool Success { get; set; }
}
