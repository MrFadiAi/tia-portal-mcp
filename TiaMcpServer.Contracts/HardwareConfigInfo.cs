using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

public class HardwareConfigInfo
{
    public List<DeviceInfo> Devices { get; set; } = new List<DeviceInfo>();

    public List<SubnetInfo> Subnets { get; set; } = new List<SubnetInfo>();

    /// <summary>
    /// Degradation/decision notes from the I/O-details extension (includeIoDetails /
    /// includeTagMatches), e.g. which PLC's tags were matched or why tag matching was skipped.
    /// Null on a default read (byte-identical payload).
    /// </summary>
    public List<string>? IoNotes { get; set; }

    /// <summary>Device name of the PLC whose tag tables were matched against channels
    /// (includeTagMatches). Null when no tag index was built.</summary>
    public string? IoTagMatchSource { get; set; }

    /// <summary>True when the I/O-details payload was capped at the entry budget and remaining
    /// device items report no ioDetails. Null unless the extension ran.</summary>
    public bool? IoDetailsTruncated { get; set; }
}
