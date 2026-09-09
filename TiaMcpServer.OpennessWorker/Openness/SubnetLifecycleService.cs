using System;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Subnet lifecycle operations (create/update/delete), mirroring the upstream Siemens API
/// routes: <c>project.Subnets.Create(typeIdentifier, name)</c>, HighestAddress via
/// <c>SetAttribute</c>, TransmissionSpeed via <c>Enum.Parse(currentType, value)</c> from the
/// current attribute value's enum type, rename via the Name property, <c>subnet.Delete()</c>.
/// Unlike upstream we deliberately skip ExclusiveAccess/Transaction ceremony (consistent with
/// our other mutations) and key subnets by NAME with an ambiguity check instead of SubnetId.
/// Every operation has a dry-run path (confirm false) that only reads.
/// </summary>
public static class SubnetLifecycleService
{
    public static SubnetLifecycleResultInfo Create(
        Project project, string name, string networkType, int? highestAddress, string? transmissionSpeed, bool confirm)
    {
        var result = new SubnetLifecycleResultInfo
        {
            Operation = "create_subnet",
            SubnetName = name,
            NetworkType = networkType,
        };

        var duplicates = FindMatches(project, name);
        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                $"A subnet named '{name}' already exists. Subnet names must be unique.");
        }

        if (!confirm)
        {
            result.Message =
                $"DRY RUN — would create a {networkType} subnet '{name}'" +
                WithPrefix(" with ", DescribeProfibusFields(highestAddress, transmissionSpeed)) + ".";
            return result;
        }

        var subnet = project.Subnets.Create(SubnetLifecycleContract.TypeIdentifierFor(networkType), name);
        ApplyProfibusAttributesIfPresent(subnet, networkType, highestAddress, transmissionSpeed);

        result.SubnetId = ReadSubnetId(subnet);
        result.Applied = true;
        result.Message =
            $"Created {networkType} subnet '{name}'" +
            WithPrefix(" with ", DescribeProfibusFields(highestAddress, transmissionSpeed)) + ".";
        return result;
    }

    public static SubnetLifecycleResultInfo Update(
        Project project, string name, string? newName, int? highestAddress, string? transmissionSpeed, bool confirm)
    {
        var result = new SubnetLifecycleResultInfo { Operation = "update_subnet", SubnetName = name };

        var subnet = ResolveUnique(project, name, out var networkType);
        result.NetworkType = networkType;
        result.SubnetId = ReadSubnetId(subnet);

        bool wantsProfibusFields = highestAddress is not null || !string.IsNullOrWhiteSpace(transmissionSpeed);
        if (wantsProfibusFields && !IsProfibus(networkType))
        {
            throw new InvalidOperationException(
                $"Subnet '{name}' is {networkType ?? "of unknown type"} — highestAddress and transmissionSpeed " +
                "are PROFIBUS-only and cannot be applied to it.");
        }

        if (!string.IsNullOrWhiteSpace(newName))
        {
            var renameDuplicates = FindMatches(project, newName!);
            if (renameDuplicates.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Cannot rename '{name}' to '{newName}': a subnet named '{newName}' already exists.");
            }
        }

        if (!confirm)
        {
            result.NewName = newName;
            result.Message = $"DRY RUN — would update subnet '{name}'" + DescribeChanges(newName, highestAddress, transmissionSpeed) + ".";
            return result;
        }

        if (!string.IsNullOrWhiteSpace(newName) && !string.Equals(newName, name, StringComparison.Ordinal))
        {
            subnet.Name = newName!;
            result.SubnetName = subnet.Name;
            result.NewName = subnet.Name;
        }

        ApplyProfibusAttributesIfPresent(subnet, networkType, highestAddress, transmissionSpeed);

        result.Applied = true;
        result.Message = $"Updated subnet '{result.SubnetName}'" + DescribeChanges(newName, highestAddress, transmissionSpeed) + ".";
        return result;
    }

    public static SubnetLifecycleResultInfo Delete(Project project, string name, bool confirm, bool force)
    {
        var result = new SubnetLifecycleResultInfo { Operation = "delete_subnet", SubnetName = name };

        var subnet = ResolveUnique(project, name, out var networkType);
        result.NetworkType = networkType;
        result.SubnetId = ReadSubnetId(subnet);

        var nodeRead = ReadConnectedNodes(subnet, name);
        result.ConnectedNodes.AddRange(nodeRead.Nodes);
        var verdict = ConnectedNodeGuard.Evaluate(nodeRead.Status, result.ConnectedNodes.Count);

        if (verdict.GuardEngaged && !force)
        {
            result.Message = verdict.RequiresManualVerification
                ? $"Refused: {ConnectedNodeGuard.UnverifiableNote} for subnet '{name}' — " +
                  "the node list could not be read, so the subnet may still have connected interfaces. " +
                  "Verify manually in TIA Portal, or re-run with force=true to delete it anyway " +
                  "(their network interfaces will become subnet-less)."
                : $"Refused: subnet '{name}' still has {result.ConnectedNodes.Count} connected node(s): " +
                  $"{string.Join(", ", result.ConnectedNodes)}. " +
                  "Disconnect them in TIA Portal first, or re-run with force=true to delete the subnet anyway " +
                  "(their network interfaces will become subnet-less).";
            return result;
        }

        if (!confirm)
        {
            result.Message =
                $"DRY RUN — would delete subnet '{name}'" +
                (verdict.RequiresManualVerification
                    ? $" (NOTE: {ConnectedNodeGuard.UnverifiableNote})"
                    : result.ConnectedNodes.Count > 0
                        ? $" and detach {result.ConnectedNodes.Count} connected node(s) ({string.Join(", ", result.ConnectedNodes)})"
                        : " (no connected nodes)") + ".";
            return result;
        }

        subnet.Delete();

        result.Applied = true;
        result.Message =
            $"Deleted subnet '{name}'" +
            (verdict.RequiresManualVerification
                ? $" (NOTE: {ConnectedNodeGuard.UnverifiableNote})"
                : result.ConnectedNodes.Count > 0
                    ? $" ({result.ConnectedNodes.Count} connected node(s) were detached)"
                    : " (no connected nodes)") + ".";
        return result;
    }

    /// <summary>Name-keyed lookup: 0 matches = not found, >1 (case-insensitive) = ambiguous. Subnet names
    /// are unique per project in TIA Portal, but the guard catches non-deterministic API states loudly.</summary>
    private static Subnet ResolveUnique(Project project, string name, out string? networkType)
    {
        var matches = FindMatches(project, name);
        if (matches.Count == 0)
        {
            var available = project.Subnets.Select(s => s.Name).Where(n => n is not null).ToList();
            throw new InvalidOperationException(
                $"Subnet '{name}' was not found." +
                (available.Count > 0 ? $" Available subnets: {string.Join(", ", available)}." : " The project has no subnets."));
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Subnet name '{name}' is ambiguous ({matches.Count} case-insensitive matches). " +
                "Rename the duplicates in TIA Portal first.");
        }

        var subnet = matches[0];
        networkType = ReadNetworkType(subnet);
        return subnet;
    }

    private static List<Subnet> FindMatches(Project project, string name)
        => project.Subnets
            .Where(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <summary>Network type as our closed vocabulary ("Ethernet"/"Profibus") or the raw identifier —
    /// mirrors HardwareConfigReader's TypeIdentifier-with-NetType-fallback read.</summary>
    private static string? ReadNetworkType(Subnet subnet)
    {
        var identifier = HardwareConfigReader.ReadPropertyOrAttribute(subnet, "TypeIdentifier", "subnet type identifier")
            ?? HardwareConfigReader.ReadPropertyOrAttribute(subnet, "NetType", "subnet network type");
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        if (identifier.Contains("Profibus", StringComparison.OrdinalIgnoreCase))
        {
            return SubnetLifecycleContract.Profibus;
        }

        if (identifier.Contains("Ethernet", StringComparison.OrdinalIgnoreCase))
        {
            return SubnetLifecycleContract.Ethernet;
        }

        return identifier;
    }

    private static bool IsProfibus(string? networkType)
        => string.Equals(networkType, SubnetLifecycleContract.Profibus, StringComparison.Ordinal);

    private static string? ReadSubnetId(Subnet subnet)
        => HardwareConfigReader.ReadPropertyOrAttribute(subnet, "SubnetId", "subnet id");

    /// <summary>
    /// Reads the subnet's connected nodes WITHOUT <see cref="HardwareConfigReader"/>'s
    /// silent-degradation contract (ReadEnumerableProperty swallows every failure into an
    /// empty sequence, which is right for reads but would silently weaken the delete guard):
    /// any failure — missing/not-enumerable Nodes property, or a Siemens exception during
    /// enumeration — is surfaced as <see cref="ConnectedNodeReadStatus.Unreadable"/> (keeping
    /// any partially collected names) so the guard engages instead of reporting a fake "0 nodes".
    /// </summary>
    private static ConnectedNodeReadResult ReadConnectedNodes(Subnet subnet, string subnetName)
    {
        var nodes = new List<string>();
        try
        {
            var nodesValue = subnet.GetType().GetProperty("Nodes")?.GetValue(subnet);
            if (nodesValue is not System.Collections.IEnumerable enumerable)
            {
                Console.Error.WriteLine(
                    $"[SUBNET] Nodes of subnet '{subnetName}' is missing or not enumerable on this TIA version — delete guard treated as engaged.");
                return ConnectedNodeReadResult.Unreadable(nodes);
            }

            foreach (var node in enumerable)
            {
                var nodeName = node.GetType().GetProperty("Name")?.GetValue(node) as string;
                if (!string.IsNullOrWhiteSpace(nodeName))
                {
                    nodes.Add(nodeName);
                }
            }

            return ConnectedNodeReadResult.Readable(nodes);
        }
        catch (Exception ex) when (ex is EngineeringException
                                      or System.Reflection.TargetInvocationException
                                      or InvalidOperationException)
        {
            Console.Error.WriteLine(
                $"[SUBNET] Failed to enumerate connected nodes of subnet '{subnetName}': {ex.Message} — delete guard treated as engaged.");
            return ConnectedNodeReadResult.Unreadable(nodes);
        }
    }

    private static void ApplyProfibusAttributesIfPresent(
        Subnet subnet, string? networkType, int? highestAddress, string? transmissionSpeed)
    {
        if (highestAddress is null && string.IsNullOrWhiteSpace(transmissionSpeed))
        {
            return;
        }

        var engineeringObject = (IEngineeringObject)subnet;
        if (highestAddress is not null)
        {
            engineeringObject.SetAttribute("HighestAddress", highestAddress.Value);
        }

        if (!string.IsNullOrWhiteSpace(transmissionSpeed))
        {
            // The TransmissionSpeed attribute is an enum whose concrete type is only known at
            // runtime (version-specific Siemens assembly) — parse the validated spelling into
            // the CURRENT value's enum type, mirroring the upstream route.
            var current = engineeringObject.GetAttribute("TransmissionSpeed");
            if (current is null)
            {
                throw new InvalidOperationException(
                    $"Cannot set transmissionSpeed: subnet '{subnet.Name}' has no readable current " +
                    "TransmissionSpeed attribute to derive the enum type from.");
            }

            object speedValue;
            try
            {
                speedValue = Enum.Parse(current.GetType(), transmissionSpeed!, ignoreCase: false);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException(
                    $"transmissionSpeed '{transmissionSpeed}' is not a value of the runtime enum " +
                    $"{current.GetType().Name} on this TIA version.", ex);
            }

            engineeringObject.SetAttribute("TransmissionSpeed", speedValue);
        }
    }

    private static string DescribeProfibusFields(int? highestAddress, string? transmissionSpeed)
    {
        var parts = new List<string>();
        if (highestAddress is not null)
        {
            parts.Add($"highestAddress={highestAddress}");
        }

        if (!string.IsNullOrWhiteSpace(transmissionSpeed))
        {
            parts.Add($"transmissionSpeed={transmissionSpeed}");
        }

        return string.Join(", ", parts);
    }

    private static string DescribeChanges(string? newName, int? highestAddress, string? transmissionSpeed)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(newName))
        {
            parts.Add($"rename to '{newName}'");
        }

        var profibus = DescribeProfibusFields(highestAddress, transmissionSpeed);
        if (profibus.Length > 0)
        {
            parts.Add(profibus);
        }

        return parts.Count > 0 ? ": " + string.Join(", ", parts) : "";
    }

    private static string WithPrefix(string prefix, string value)
        => value.Length > 0 ? prefix + value : "";
}
