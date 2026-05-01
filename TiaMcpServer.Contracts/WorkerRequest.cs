namespace TiaMcpServer.Contracts;

public class WorkerRequest
{
    public string Method { get; set; } = string.Empty;

    public string? ProjectPath { get; set; }

    public string? BlockPath { get; set; }

    public string? YamlContent { get; set; }

    public string? PlcName { get; set; }

    public string? CrossReferenceFilter { get; set; }

    public bool AllowTiaConfirmations { get; set; }

    public string? Query { get; set; }

    public string? TypeIdentifier { get; set; }

    public string? DeviceName { get; set; }

    public string? DeviceItemName { get; set; }

    public string? IpAddress { get; set; }

    public string? SubnetMask { get; set; }

    public string? PnDeviceName { get; set; }

    public string? SubnetName { get; set; }

    public string? IoSystemName { get; set; }

    public bool Confirm { get; set; }
}
