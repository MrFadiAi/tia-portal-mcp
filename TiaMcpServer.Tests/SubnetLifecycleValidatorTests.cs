using System.Linq;
using TiaMcpServer.Contracts;
using TiaMcpServer.OpennessWorker.Openness;
using Xunit;

namespace TiaMcpServer.Tests;

/// <summary>
/// Pure request validation for the subnet lifecycle tools. These run BEFORE a worker session
/// is opened — an invalid request must never touch TIA Portal. Live-state checks (PROFIBUS
/// fields on an existing Ethernet subnet during update) are deliberately NOT here.
/// </summary>
public class SubnetLifecycleValidatorTests
{
    // ---- create_subnet ------------------------------------------------------

    [Fact]
    public void CreateEthernetValidWithNoProfibusFields()
    {
        var errors = SubnetLifecycleValidator.ValidateCreate("PN_1", "Ethernet", null, null);

        Assert.Empty(errors);
    }

    [Fact]
    public void CreateProfibusValidWithBothProfibusFields()
    {
        var errors = SubnetLifecycleValidator.ValidateCreate("PB_1", "Profibus", 126, "Baud187500");

        Assert.Empty(errors);
    }

    [Fact]
    public void CreateRequiresName()
    {
        var errors = SubnetLifecycleValidator.ValidateCreate("  ", "Ethernet", null, null);

        Assert.Contains(errors, e => e.Contains("Subnet name is required"));
    }

    [Fact]
    public void CreateRequiresNetworkType()
    {
        var errors = SubnetLifecycleValidator.ValidateCreate("PN_1", null, null, null);

        Assert.Contains(errors, e => e.Contains("networkType is required"));
    }

    [Fact]
    public void CreateRejectsUnknownNetworkType()
    {
        var errors = SubnetLifecycleValidator.ValidateCreate("PN_1", "Profinet", null, null);

        Assert.Contains(errors, e => e.Contains("'Profinet' is not supported"));
    }

    [Fact]
    public void CreateRejectsProfibusFieldsOnEthernet()
    {
        var errors = SubnetLifecycleValidator.ValidateCreate("PN_1", "Ethernet", 126, null);
        var speedErrors = SubnetLifecycleValidator.ValidateCreate("PN_1", "Ethernet", null, "Baud9600");

        Assert.Contains(errors, e => e.Contains("PROFIBUS-only"));
        Assert.Contains(speedErrors, e => e.Contains("PROFIBUS-only"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(127)]
    public void CreateProfibusRejectsHighestAddressOutOfRange(int highestAddress)
    {
        var errors = SubnetLifecycleValidator.ValidateCreate("PB_1", "Profibus", highestAddress, null);

        Assert.Contains(errors, e => e.Contains("highestAddress must be 0-126"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(126)]
    public void CreateProfibusAcceptsHighestAddressBounds(int highestAddress)
    {
        var errors = SubnetLifecycleValidator.ValidateCreate("PB_1", "Profibus", highestAddress, null);

        Assert.Empty(errors);
    }

    [Fact]
    public void CreateProfibusRejectsUnknownTransmissionSpeed()
    {
        var errors = SubnetLifecycleValidator.ValidateCreate("PB_1", "Profibus", null, "Baud123");

        Assert.Contains(errors, e => e.Contains("'Baud123' is not supported"));
    }

    // ---- update_subnet ------------------------------------------------------

    [Fact]
    public void UpdateValidWithSingleChange()
    {
        var errors = SubnetLifecycleValidator.ValidateUpdate("PN_1", "PN_2", null, null);

        Assert.Empty(errors);
    }

    [Fact]
    public void UpdateValidWithProfibusFields()
    {
        var errors = SubnetLifecycleValidator.ValidateUpdate("PB_1", null, 32, "Baud500000");

        Assert.Empty(errors);
    }

    [Fact]
    public void UpdateRequiresName()
    {
        var errors = SubnetLifecycleValidator.ValidateUpdate(null, "PN_2", null, null);

        Assert.Contains(errors, e => e.Contains("Subnet name is required"));
    }

    [Fact]
    public void UpdateRequiresAtLeastOneChange()
    {
        var errors = SubnetLifecycleValidator.ValidateUpdate("PN_1", null, null, null);

        Assert.Contains(errors, e => e.Contains("at least one change"));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(127)]
    public void UpdateRejectsHighestAddressOutOfRange(int highestAddress)
    {
        var errors = SubnetLifecycleValidator.ValidateUpdate("PB_1", null, highestAddress, null);

        Assert.Contains(errors, e => e.Contains("highestAddress must be 0-126"));
    }

    [Fact]
    public void UpdateRejectsUnknownTransmissionSpeed()
    {
        var errors = SubnetLifecycleValidator.ValidateUpdate("PB_1", null, null, "fast");

        Assert.Contains(errors, e => e.Contains("'fast' is not supported"));
    }

    // ---- delete_subnet ------------------------------------------------------

    [Fact]
    public void DeleteValidWithName()
    {
        Assert.Empty(SubnetLifecycleValidator.ValidateDelete("PN_1"));
    }

    [Fact]
    public void DeleteRequiresName()
    {
        var errors = SubnetLifecycleValidator.ValidateDelete("");

        Assert.Contains(errors, e => e.Contains("Subnet name is required"));
    }

    // ---- contract vocabulary ------------------------------------------------

    [Fact]
    public void TypeIdentifierMapsNetworkTypes()
    {
        Assert.Equal("System:Subnet.Ethernet", SubnetLifecycleContract.TypeIdentifierFor("Ethernet"));
        Assert.Equal("System:Subnet.Profibus", SubnetLifecycleContract.TypeIdentifierFor("Profibus"));
    }
}
