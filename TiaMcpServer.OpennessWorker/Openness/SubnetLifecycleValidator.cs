using System.Collections.Generic;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Pure request validation for the subnet lifecycle operations (Siemens-free, TDD'd). The
/// worker handler runs this BEFORE opening a session — an invalid request never touches TIA
/// Portal. Checks that need live project state (e.g. PROFIBUS-only fields on an Ethernet
/// subnet during update) live in the service, not here.
/// </summary>
public static class SubnetLifecycleValidator
{
    /// <summary>
    /// create_subnet: name + supported networkType required; highestAddress (0-126) and
    /// transmissionSpeed are PROFIBUS-only — an Ethernet create carrying either is rejected.
    /// </summary>
    public static IReadOnlyList<string> ValidateCreate(
        string? name, string? networkType, int? highestAddress, string? transmissionSpeed)
    {
        var errors = new List<string>();

        RequireName(name, errors);

        if (string.IsNullOrWhiteSpace(networkType))
        {
            errors.Add("networkType is required: Ethernet or Profibus.");
        }
        else if (!SubnetLifecycleContract.IsSupportedNetworkType(networkType))
        {
            errors.Add(
                $"networkType '{networkType}' is not supported. Valid values: " +
                $"{SubnetLifecycleContract.Ethernet}, {SubnetLifecycleContract.Profibus}.");
        }
        else if (string.Equals(networkType, SubnetLifecycleContract.Ethernet, System.StringComparison.Ordinal))
        {
            RejectProfibusFieldsForEthernet("create_subnet", highestAddress, transmissionSpeed, errors);
        }
        else
        {
            ValidateProfibusFields(highestAddress, transmissionSpeed, errors);
        }

        return errors;
    }

    /// <summary>
    /// update_subnet: name + at least one change required (a network type is fixed at creation
    /// and cannot be updated). PROFIBUS fields are validated for spelling/range; their
    /// applicability to the target's actual type is checked by the service against live state.
    /// </summary>
    public static IReadOnlyList<string> ValidateUpdate(
        string? name, string? newName, int? highestAddress, string? transmissionSpeed)
    {
        var errors = new List<string>();

        RequireName(name, errors);

        if (string.IsNullOrWhiteSpace(newName) && highestAddress is null && string.IsNullOrWhiteSpace(transmissionSpeed))
        {
            errors.Add(
                "update_subnet needs at least one change: newName, highestAddress or transmissionSpeed. " +
                "(A subnet's network type is fixed at creation and cannot be updated.)");
        }

        if (!string.IsNullOrWhiteSpace(newName) && newName!.Trim().Length == 0)
        {
            errors.Add("newName must not be blank.");
        }

        ValidateProfibusFields(highestAddress, transmissionSpeed, errors);

        return errors;
    }

    /// <summary>delete_subnet: name required.</summary>
    public static IReadOnlyList<string> ValidateDelete(string? name)
    {
        var errors = new List<string>();
        RequireName(name, errors);
        return errors;
    }

    private static void RequireName(string? name, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("Subnet name is required.");
        }
    }

    private static void RejectProfibusFieldsForEthernet(
        string operation, int? highestAddress, string? transmissionSpeed, List<string> errors)
    {
        bool hasProfibusField = highestAddress is not null || !string.IsNullOrWhiteSpace(transmissionSpeed);
        if (hasProfibusField)
        {
            errors.Add(
                $"{operation}: highestAddress and transmissionSpeed are PROFIBUS-only and were rejected " +
                $"for an {SubnetLifecycleContract.Ethernet} subnet.");
        }
    }

    private static void ValidateProfibusFields(int? highestAddress, string? transmissionSpeed, List<string> errors)
    {
        if (highestAddress is < SubnetLifecycleContract.MinimumHighestAddress
            || highestAddress is > SubnetLifecycleContract.MaximumHighestAddress)
        {
            errors.Add(
                $"highestAddress must be {SubnetLifecycleContract.MinimumHighestAddress}-" +
                $"{SubnetLifecycleContract.MaximumHighestAddress} (PROFIBUS highest station address).");
        }

        if (!string.IsNullOrWhiteSpace(transmissionSpeed)
            && !SubnetLifecycleContract.IsSupportedTransmissionSpeed(transmissionSpeed))
        {
            errors.Add(
                $"transmissionSpeed '{transmissionSpeed}' is not supported. Valid values: " +
                $"{string.Join(", ", SubnetLifecycleContract.TransmissionSpeeds)}.");
        }
    }
}
