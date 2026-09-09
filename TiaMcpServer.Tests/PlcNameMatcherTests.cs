using System.Linq;
using TiaMcpServer.OpennessWorker.Openness;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// PLC-name resolution must be uniform across every tool taking a plcName: EITHER the device
/// name OR the PLC-software name is accepted, case-insensitively (live evidence: list_plc_types
/// accepted the software name "PLC DIG TWIN" but get_type_content rejected it because it only
/// matched device names). A miss must error listing BOTH name forms so the user can copy either.
/// </summary>
public class PlcNameMatcherTests
{
    [Fact]
    public void MatchesTheDeviceName()
    {
        Assert.True(PlcNameMatcher.NameMatches("S7-1500 station_1", "PLUKROBOT", "S7-1500 station_1"));
    }

    [Fact]
    public void MatchesTheSoftwareName()
    {
        Assert.True(PlcNameMatcher.NameMatches("S7-1500 station_1", "PLUKROBOT", "PLUKROBOT"));
    }

    [Theory]
    [InlineData("plukrobot")]        // software name, different case
    [InlineData("PLUKROBOT")]
    [InlineData("s7-1500 STATION_1")] // device name, different case
    public void MatchingIsCaseInsensitive(string requested)
    {
        Assert.True(PlcNameMatcher.NameMatches("S7-1500 station_1", "PLUKROBOT", requested));
    }

    [Theory]
    [InlineData("PLC DIG TWIN")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void NonMatchingOrNullRequestDoesNotMatch(string? requested)
    {
        Assert.False(PlcNameMatcher.NameMatches("S7-1500 station_1", "PLUKROBOT", requested));
    }

    [Fact]
    public void NotFoundMessageListsBothNameForms()
    {
        var candidates = new[]
        {
            (DeviceName: "S7-1500 station_1", SoftwareName: "PLUKROBOT"),
            (DeviceName: "ET200 station_2", SoftwareName: "PLC DIG TWIN"),
        };

        var message = PlcNameMatcher.BuildNotFoundMessage("nope", candidates);

        Assert.Contains("PLC 'nope' not found", message);
        Assert.Contains("PLUKROBOT", message);
        Assert.Contains("S7-1500 station_1", message);
        Assert.Contains("PLC DIG TWIN", message);
        Assert.Contains("ET200 station_2", message);
    }

    [Fact]
    public void AmbiguousMessageCountsMatchesAndListsCandidates()
    {
        var candidates = new[]
        {
            (DeviceName: "station_A", SoftwareName: "sameName"),
            (DeviceName: "sameName", SoftwareName: "other"),
        };

        var message = PlcNameMatcher.BuildAmbiguousMessage("sameName", 2, candidates);

        Assert.Contains("ambiguous", message);
        Assert.Contains("2", message);
        Assert.Contains("station_A", message);
        Assert.Contains("other", message);
    }

    [Fact]
    public void DescribeAvailableHandlesEmptyProject()
    {
        Assert.Equal(" The project has no PLCs.", PlcNameMatcher.DescribeAvailable(Enumerable.Empty<(string?, string?)>()));
    }
}
