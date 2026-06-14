namespace TiaMcpServer.Contracts;

public class PlcInventoryInfo
{
    /// <summary>The container device name (TIA Portal device, e.g. "PLF_01A_PLC_CARROUSEL_2").</summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>The PLC software name (the CPU/SW container, e.g. "PLF_03A_PLC_CARROUSEL").
    /// Often differs from DeviceName — pass either to plcName filters.</summary>
    public string PlcName { get; set; } = string.Empty;

    public int BlockCount { get; set; }

    public int TagTableCount { get; set; }

    public int TypeCount { get; set; }
}
