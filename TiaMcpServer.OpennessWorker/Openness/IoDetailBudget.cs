namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Pure per-response entry budget for the read_hardware_config I/O-details extension, so a large
/// multi-rack project cannot blow the tool payload: device items contribute addresses + channels
/// until the budget is used up, after which further items report no ioDetails and
/// <see cref="Truncated"/> is set (surfaced as HardwareConfigInfo.IoDetailsTruncated).
/// Siemens-free; link-compiled into the test project.
/// </summary>
public sealed class IoDetailBudget
{
    public const int DefaultMaxEntries = 3000;

    public IoDetailBudget(int maxEntries = DefaultMaxEntries)
    {
        MaxEntries = maxEntries;
    }

    public int MaxEntries { get; }

    public int UsedEntries { get; private set; }

    /// <summary>True once the budget has been consumed; remaining device items get no ioDetails.</summary>
    public bool Truncated => UsedEntries >= MaxEntries;

    /// <summary>Whether another device item may still contribute I/O details. The item that
    /// EXHAUSTS the budget is still read in full (so truncation never yields a partial item).</summary>
    public bool CanTake() => UsedEntries < MaxEntries;

    /// <summary>Record the entries consumed by one fully-read device item.</summary>
    public void Take(int entries)
    {
        UsedEntries += entries;
        if (UsedEntries > MaxEntries)
        {
            UsedEntries = MaxEntries;
        }
    }
}
