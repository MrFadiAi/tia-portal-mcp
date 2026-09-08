using System.Text.Json;
using System.Text.Json.Serialization;

namespace TiaMcpServer.Diagnostics;

/// <summary>Machine-readable rendering (the doctor MCP tool's format:"json"). Round-trippable
/// via <see cref="Parse"/> — the JSON shape and the record shape carry the same information.</summary>
public static class DoctorJsonRenderer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Render(DoctorReport report)
    {
        var (ok, info, warn, fail) = DoctorCounts.Of(report);
        return JsonSerializer.Serialize(new
        {
            report.Status,
            report.TimestampUtc,
            report.HostVersion,
            report.Checks,
            counts = new { ok, info, warn, fail }
        }, Options);
    }

    public static DoctorReport? Parse(string json)
    {
        var dto = JsonSerializer.Deserialize<DoctorReportDto>(json, Options);
        if (dto is null)
        {
            return null;
        }

        return new DoctorReport(dto.Status, dto.TimestampUtc, dto.HostVersion, dto.Checks);
    }

    private sealed record DoctorReportDto(
        DiagnosticStatus Status,
        DateTimeOffset TimestampUtc,
        string HostVersion,
        List<DiagnosticCheckResult> Checks);
}
