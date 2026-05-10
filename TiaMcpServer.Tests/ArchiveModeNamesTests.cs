using TiaMcpServer.Contracts;
using Xunit;

namespace TiaMcpServer.Tests;

public class ArchiveModeNamesTests
{
    [Fact]
    public void EmptyArchiveModeDefaultsToCompressed()
    {
        Assert.True(ArchiveModeNames.TryNormalize(null, out var normalized, out var error));

        Assert.Equal(ArchiveModeNames.Compressed, normalized);
        Assert.Null(error);
    }

    [Fact]
    public void ArchiveModeNormalizesIgnoringCase()
    {
        Assert.True(ArchiveModeNames.TryNormalize("discardrestorabledata", out var normalized, out var error));

        Assert.Equal(ArchiveModeNames.DiscardRestorableData, normalized);
        Assert.Null(error);
    }

    [Fact]
    public void InvalidArchiveModeReturnsError()
    {
        Assert.False(ArchiveModeNames.TryNormalize("Zip", out _, out var error));

        Assert.Contains("Invalid archive mode", error);
    }
}
