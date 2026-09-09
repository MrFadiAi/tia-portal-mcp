using TiaMcpServer.OpennessWorker.Openness;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// delete_subnet's connected-node guard must never silently weaken on older TIA versions: an
/// unreadable Nodes read ENGAGES the guard (refuse without force + manual-verification note);
/// only a successfully-read empty collection counts as "0 connected nodes".
/// </summary>
public class ConnectedNodeGuardTests
{
    [Fact]
    public void UnreadableCheckEngagesGuardAndRequiresManualVerification()
    {
        var verdict = ConnectedNodeGuard.Evaluate(ConnectedNodeReadStatus.Unreadable, readableNodeCount: 0);

        Assert.True(verdict.GuardEngaged);
        Assert.True(verdict.RequiresManualVerification);
    }

    [Fact]
    public void UnreadableCheckEngagesGuardEvenWithPartiallyCollectedNodes()
    {
        // A partial enumeration (some names collected, then failure) is still UNVERIFIED.
        var verdict = ConnectedNodeGuard.Evaluate(ConnectedNodeReadStatus.Unreadable, readableNodeCount: 3);

        Assert.True(verdict.GuardEngaged);
        Assert.True(verdict.RequiresManualVerification);
    }

    [Fact]
    public void ReadableEmptyCollectionIsTheOnlyPassThrough()
    {
        var verdict = ConnectedNodeGuard.Evaluate(ConnectedNodeReadStatus.Readable, readableNodeCount: 0);

        Assert.False(verdict.GuardEngaged);
        Assert.False(verdict.RequiresManualVerification);
    }

    [Fact]
    public void ReadableNodesEngageGuardWithoutTheUnverifiableNote()
    {
        var verdict = ConnectedNodeGuard.Evaluate(ConnectedNodeReadStatus.Readable, readableNodeCount: 2);

        Assert.True(verdict.GuardEngaged);
        Assert.False(verdict.RequiresManualVerification);
    }

    [Fact]
    public void ResultFactoriesCarryStatusAndNodes()
    {
        var readable = ConnectedNodeReadResult.Readable(new[] { "PLC_1" });
        Assert.Equal(ConnectedNodeReadStatus.Readable, readable.Status);
        Assert.Single(readable.Nodes);

        var unreadable = ConnectedNodeReadResult.Unreadable(new[] { "PLC_1", "HMI_1" });
        Assert.Equal(ConnectedNodeReadStatus.Unreadable, unreadable.Status);
        Assert.Equal(2, unreadable.Nodes.Count);
    }
}
