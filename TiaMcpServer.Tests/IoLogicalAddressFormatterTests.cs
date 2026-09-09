using TiaMcpServer.Contracts;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// Pure parsing/formatting of Siemens absolute I/O addresses. The formatter is the identity
/// behind channel↔tag matching: the same numbers in a different area (or a different width) are
/// a DIFFERENT identity, and nothing is ever fabricated from partial evidence.
/// </summary>
public class IoLogicalAddressFormatterTests
{
    [Theory]
    [InlineData("%I4.0", "I", 32, 1u)]      // bit: byte 4 bit 0 → start bit 32
    [InlineData("%Q4.0", "Q", 32, 1u)]
    [InlineData("%i7.7", "I", 63, 1u)]      // casing normalized
    [InlineData(" %IB4 ", "I", 32, 8u)]     // whitespace + byte
    [InlineData("%QB10", "Q", 80, 8u)]
    [InlineData("%IW64", "I", 512, 16u)]    // word
    [InlineData("%QW64", "Q", 512, 16u)]
    [InlineData("%ID64", "I", 512, 32u)]    // dword
    [InlineData("%QD64", "Q", 512, 32u)]
    public void TryParseParsesSupportedSpellings(string text, string expectedArea, int expectedStartBit, uint expectedBitCount)
    {
        Assert.True(IoLogicalAddressFormatter.TryParse(text, out var address));

        Assert.Equal(expectedArea, address!.Value.Area);
        Assert.Equal(expectedStartBit, address.Value.Interval.StartBit);
        Assert.Equal(expectedBitCount, address.Value.Interval.BitCount);
    }

    [Theory]
    [InlineData("%M4.0")]     // memory area — not an I/O address
    [InlineData("DB5.DBX4.0")]
    [InlineData("%IW63")]     // word must start on an even byte
    [InlineData("%ID62")]     // dword must start on a byte divisible by 4
    [InlineData("%I4.8")]     // bit is 0-7
    [InlineData("%I4.-1")]
    [InlineData("I4.0")]      // missing % prefix
    [InlineData("%X4.0")]     // unknown area letter
    [InlineData("MySensor")]  // symbolic
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryParseRejectsUnsupportedOrMisalignedText(string? text)
    {
        Assert.False(IoLogicalAddressFormatter.TryParse(text, out var address));
        Assert.Null(address);
    }

    [Fact]
    public void RoundTripParsesWhatItFormats()
    {
        foreach (var formatted in new[] { "%I4.0", "%QB10", "%QW64", "%ID64" })
        {
            Assert.True(IoLogicalAddressFormatter.TryParse(formatted, out var parsed));
            var reFormatted = IoLogicalAddressFormatter.FormatLogicalAddress(
                parsed!.Value.Area == "I" ? "Input" : "Output",
                parsed.Value.Interval.StartBit,
                parsed.Value.Interval.BitCount);

            Assert.Equal(formatted, reFormatted);
        }
    }

    [Theory]
    [InlineData("Input", 32, 1u, "%I4.0")]
    [InlineData("Output", 80, 8u, "%QB10")]
    [InlineData("Input", 512, 16u, "%IW64")]
    [InlineData("Output", 512, 32u, "%QD64")]
    public void FormatLogicalAddressFormatsAlignedEvidence(string ioType, int startBit, uint widthBits, string expected)
        => Assert.Equal(expected, IoLogicalAddressFormatter.FormatLogicalAddress(ioType, startBit, widthBits));

    [Theory]
    [InlineData("Input", 33, 8u)]      // byte width at a non-byte boundary
    [InlineData("Input", 488, 16u)]    // word at an odd byte (61)
    [InlineData("Input", 496, 32u)]    // dword at byte 62 (not % 4 == 0)
    [InlineData("Complex", 32, 1u)]    // complex channels have no absolute I/O address
    [InlineData(null, 32, 1u)]
    [InlineData("Input", null, 1u)]    // partial evidence — never a guess
    [InlineData("Input", 32, null)]
    [InlineData("Input", -1, 1u)]
    [InlineData("Input", 32, 12u)]     // unsupported width
    public void FormatLogicalAddressReturnsNullForIncompleteOrMisalignedEvidence(
        string? ioType, int? startBit, uint? widthBits)
        => Assert.Null(IoLogicalAddressFormatter.FormatLogicalAddress(ioType, startBit, widthBits));

    [Theory]
    [InlineData("Input", "I")]
    [InlineData("input", "I")]
    [InlineData(" Output ", "Q")]
    [InlineData("OUTPUT", "Q")]
    [InlineData("Substitute", null)]
    [InlineData("Diagnosis", null)]
    [InlineData("Complex", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeAreaMapsOnlyInputAndOutput(string? opennessIoType, string? expected)
        => Assert.Equal(expected, IoLogicalAddressFormatter.NormalizeArea(opennessIoType));
}
