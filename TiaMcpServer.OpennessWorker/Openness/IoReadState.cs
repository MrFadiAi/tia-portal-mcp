using System.Collections.Generic;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Threading state for the read_hardware_config I/O-details extension: whether details are read
/// at all, the resolved tag index (null = no matching), the shared notes list, and the per-response
/// entry budget. A null IoReadState anywhere means "legacy read" — byte-identical output.
/// </summary>
public sealed class IoReadState
{
    private bool _truncationNoted;

    public IoReadState(IoTagIndex? tagIndex, List<string> notes, IoDetailBudget budget)
    {
        TagIndex = tagIndex;
        Notes = notes;
        Budget = budget;
    }

    public IoTagIndex? TagIndex { get; }

    public List<string> Notes { get; }

    public IoDetailBudget Budget { get; }

    /// <summary>Record the truncation note exactly once, after the budget is first exhausted.</summary>
    public void NoteTruncationOnce()
    {
        if (Budget.Truncated && !_truncationNoted)
        {
            _truncationNoted = true;
            Notes.Add(
                $"I/O details truncated after {Budget.UsedEntries} entries (budget {Budget.MaxEntries}); " +
                "remaining device items report no ioDetails.");
        }
    }
}
