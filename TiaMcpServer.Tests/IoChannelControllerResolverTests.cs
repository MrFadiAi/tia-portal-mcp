using System.Collections.Generic;
using TiaMcpServer.Contracts;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// Pure controller resolution: a channel's tag matches are reported ONLY when the channel's
/// absolute interval is contained by exactly one address record that names exactly one owning
/// device. This is what prevents matching against another PLC's coincidentally-equal addresses.
/// </summary>
public class IoChannelControllerResolverTests
{
    private static IoAddressRecord Address(string ioType, int startByte, int length, params string[] controllers)
        => new()
        {
            IoType = ioType,
            StartAddress = startByte,
            Length = length,
            ControllerNames = controllers,
        };

    [Fact]
    public void ResolvesChannelInsideSingleAddressWithSingleController()
    {
        var result = IoChannelControllerResolver.Resolve(
            "Input", 32, 1,
            new[] { Address("Input", 4, 1, "PLC_1") },
            "DI 16x24VDC");

        Assert.Equal(IoChannelControllerStatus.Resolved, result.Status);
        Assert.Equal("PLC_1", result.ControllerName);
        Assert.Null(result.DiagnosticMessage);
        Assert.True(result.IsTargetMatch("PLC_1"));
        Assert.False(result.IsTargetMatch("PLC_2"));
    }

    [Fact]
    public void AddressBytesConvertToBits()
    {
        // Address starting at byte 8 with length 2 bytes covers bits 64..79.
        var result = IoChannelControllerResolver.Resolve(
            "Output", 72, 8,
            new[] { Address("Output", 8, 2, "PLC_1") },
            "DQ 8x24VDC");

        Assert.Equal(IoChannelControllerStatus.Resolved, result.Status);
    }

    [Theory]
    [InlineData(null, 32, 1u)]      // no I/O type
    [InlineData("Complex", 32, 1u)] // complex channels have no absolute address
    [InlineData("Input", null, 1u)]
    [InlineData("Input", 32, null)]
    [InlineData("Input", -1, 1u)]
    [InlineData("Input", 32, 0u)]   // zero width is not an interval
    public void MissingOrInvalidEvidenceYieldsNoInterval(string? ioType, int? addressBits, uint? widthBits)
    {
        var result = IoChannelControllerResolver.Resolve(
            ioType, addressBits, widthBits,
            new[] { Address("Input", 4, 1, "PLC_1") },
            "DI 16x24VDC");

        Assert.Equal(IoChannelControllerStatus.NoInterval, result.Status);
        Assert.False(result.IsTargetMatch("PLC_1"));
    }

    [Fact]
    public void AreaMismatchIsNotContainment()
    {
        var result = IoChannelControllerResolver.Resolve(
            "Input", 32, 1,
            new[] { Address("Output", 4, 1, "PLC_1") },
            "DI 16x24VDC");

        Assert.Equal(IoChannelControllerStatus.NoContainingAddress, result.Status);
        Assert.NotNull(result.DiagnosticMessage);
    }

    [Fact]
    public void ChannelOutsideEveryAddressHasNoContainingAddress()
    {
        var result = IoChannelControllerResolver.Resolve(
            "Input", 4096, 1,
            new[] { Address("Input", 4, 1, "PLC_1") },
            "DI 16x24VDC");

        Assert.Equal(IoChannelControllerStatus.NoContainingAddress, result.Status);
    }

    [Fact]
    public void OverlappingAddressesAreAmbiguous()
    {
        var result = IoChannelControllerResolver.Resolve(
            "Input", 32, 1,
            new[]
            {
                Address("Input", 4, 1, "PLC_1"),
                Address("Input", 4, 4, "PLC_1"),
            },
            "DI 16x24VDC");

        Assert.Equal(IoChannelControllerStatus.MultipleContainingAddresses, result.Status);
        Assert.NotNull(result.DiagnosticMessage);
    }

    [Fact]
    public void IncompleteAddressRecordsAreIgnored()
    {
        var incomplete = new IoAddressRecord { IoType = "Input", StartAddress = 4, Length = null };

        var result = IoChannelControllerResolver.Resolve(
            "Input", 32, 1, new[] { incomplete }, "DI 16x24VDC");

        Assert.Equal(IoChannelControllerStatus.NoContainingAddress, result.Status);
    }

    [Fact]
    public void UnreadableControllerAssociationRefusesToMatch()
    {
        var unreadableRecord = Address("Input", 4, 1);
        unreadableRecord.ControllerAssociationReadable = false;

        var result = IoChannelControllerResolver.Resolve(
            "Input", 32, 1, new[] { unreadableRecord }, "DI 16x24VDC");

        Assert.Equal(IoChannelControllerStatus.UnreadableController, result.Status);
    }

    [Fact]
    public void NoControllerRefusesToMatch()
    {
        var result = IoChannelControllerResolver.Resolve(
            "Input", 32, 1,
            new[] { Address("Input", 4, 1) },
            "DI 16x24VDC");

        Assert.Equal(IoChannelControllerStatus.NoController, result.Status);
    }

    [Fact]
    public void MultipleControllersRefuseToMatch()
    {
        var result = IoChannelControllerResolver.Resolve(
            "Input", 32, 1,
            new[] { Address("Input", 4, 1, "PLC_1", "PLC_2") },
            "DI 16x24VDC");

        Assert.Equal(IoChannelControllerStatus.MultipleControllers, result.Status);
    }
}
