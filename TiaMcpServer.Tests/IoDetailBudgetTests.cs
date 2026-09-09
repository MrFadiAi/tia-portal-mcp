using TiaMcpServer.OpennessWorker.Openness;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// The per-response entry budget bounds the includeIoDetails payload on large projects. The item
/// that exhausts the budget is still read in full; only SUBSEQUENT items are skipped.
/// </summary>
public class IoDetailBudgetTests
{
    [Fact]
    public void TakesEntriesUntilMax()
    {
        var budget = new IoDetailBudget(maxEntries: 10);

        Assert.True(budget.CanTake());
        budget.Take(10);

        Assert.True(budget.Truncated);
        Assert.False(budget.CanTake());
    }

    [Fact]
    public void ExhaustingItemIsTakenInFull()
    {
        var budget = new IoDetailBudget(maxEntries: 10);

        // The item that crosses the line still contributes everything it read.
        budget.Take(14);

        Assert.True(budget.Truncated);
        Assert.Equal(10, budget.UsedEntries); // clamped, never above max
    }

    [Fact]
    public void ZeroBudgetTruncatesImmediately()
    {
        var budget = new IoDetailBudget(maxEntries: 0);

        Assert.True(budget.Truncated);
        Assert.False(budget.CanTake());
    }

    [Fact]
    public void DefaultBudgetIsBounded()
    {
        var budget = new IoDetailBudget();

        Assert.Equal(IoDetailBudget.DefaultMaxEntries, budget.MaxEntries);
        Assert.False(budget.Truncated);
    }
}
