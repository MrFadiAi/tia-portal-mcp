using System.Linq;
using TiaMcpServer.Contracts;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// The tag index groups candidates by normalized absolute I/O identity and matches EXACTLY:
/// same area + same start bit + same width. Tags without a parsable address never participate.
/// </summary>
public class IoTagIndexTests
{
    private static IoTagCandidate Candidate(
        string name, string address, string tableName = "IO", string folderPath = "/") =>
        new(name, "Bool", address, tableName, folderPath);

    [Fact]
    public void GroupsCandidatesByAddress()
    {
        var index = new IoTagIndex("PLC_1", new[]
        {
            Candidate("Sensor1", "%I4.0", "TableB"),
            Candidate("Sensor2", "%I4.0", "TableA"),
            Candidate("Other", "%Q4.0"),
        });

        var matches = index.FindMatches(new IoAbsoluteIoAddress("I", new IoAbsoluteBitInterval(32, 1)));

        Assert.Equal(2, matches.Count);
        // Deterministic ordering: table, then folder, then name.
        Assert.Equal("Sensor2", matches[0].Name);
        Assert.Equal("Sensor1", matches[1].Name);
    }

    [Fact]
    public void MatchIdentityIncludesArea()
    {
        var index = new IoTagIndex("PLC_1", new[] { Candidate("Out", "%Q4.0") });

        var inputAddress = new IoAbsoluteIoAddress("I", new IoAbsoluteBitInterval(32, 1));
        Assert.Empty(index.FindMatches(inputAddress));
    }

    [Fact]
    public void MatchIdentityIncludesWidth()
    {
        var index = new IoTagIndex("PLC_1", new[] { Candidate("Byte0", "%IB4") });

        // Same start bit, different width: a different identity.
        var bitAddress = new IoAbsoluteIoAddress("I", new IoAbsoluteBitInterval(32, 1));
        Assert.Empty(index.FindMatches(bitAddress));
    }

    [Fact]
    public void UnparsableCandidateAddressesAreSkipped()
    {
        var index = new IoTagIndex("PLC_1", new[]
        {
            Candidate("Symbolic", "MySensor"),
            Candidate("Memory", "%M4.0"),
            Candidate("Valid", "%I4.0"),
        });

        Assert.Single(index.Candidates.Where(c => c.Name == "Valid").SelectMany(_ => index.FindMatches(
            new IoAbsoluteIoAddress("I", new IoAbsoluteBitInterval(32, 1)))));
        Assert.Equal(3, index.Candidates.Count); // candidates preserved; only matching skips
    }

    [Fact]
    public void UnknownAddressYieldsNoMatches()
    {
        var index = new IoTagIndex("PLC_1", new[] { Candidate("Sensor1", "%I4.0") });

        Assert.Empty(index.FindMatches(new IoAbsoluteIoAddress("I", new IoAbsoluteBitInterval(4096, 1))));
    }
}
