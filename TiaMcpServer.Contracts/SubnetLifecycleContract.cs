using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Contracts;

/// <summary>
/// Siemens-free closed vocabulary shared by the subnet lifecycle request surface
/// (<c>create_subnet</c>, <c>update_subnet</c>, <c>delete_subnet</c>): supported network
/// types, the PROFIBUS highest-station-address range, and the supported transmission speeds.
/// Mirrors the upstream contract; the pure validator (<c>SubnetLifecycleValidator</c>) and the
/// worker service both key on these constants instead of string literals.
/// </summary>
public static class SubnetLifecycleContract
{
    public const string Ethernet = "Ethernet";
    public const string Profibus = "Profibus";
    public const int MinimumHighestAddress = 0;
    public const int MaximumHighestAddress = 126;

    public static IReadOnlyList<string> TransmissionSpeeds { get; } = new[]
    {
        "Baud9600", "Baud19200", "Baud45450", "Baud93750", "Baud187500",
        "Baud500000", "Baud1500000", "Baud3000000", "Baud6000000", "Baud12000000",
    };

    public static bool IsSupportedNetworkType(string? value)
        => string.Equals(value, Ethernet, StringComparison.Ordinal)
            || string.Equals(value, Profibus, StringComparison.Ordinal);

    public static bool IsSupportedTransmissionSpeed(string? value)
        => value is not null && TransmissionSpeeds.Contains(value, StringComparer.Ordinal);

    /// <summary>Openness type identifier for a subnet of the given (validated) network type.</summary>
    public static string TypeIdentifierFor(string networkType)
        => string.Equals(networkType, Ethernet, StringComparison.Ordinal)
            ? "System:Subnet.Ethernet"
            : "System:Subnet.Profibus";
}
