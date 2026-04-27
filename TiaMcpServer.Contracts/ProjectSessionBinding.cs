using System;

namespace TiaMcpServer.Contracts;

public sealed class ProjectSessionBinding
{
    private string? _boundProjectPath;

    public ProjectSessionBinding(string? startupProjectPath)
    {
        _boundProjectPath = Normalize(startupProjectPath);
    }

    public string? BoundProjectPath => _boundProjectPath;

    public bool TryResolve(string? requestedProjectPath, out string? effectiveProjectPath, out string? error)
    {
        effectiveProjectPath = null;
        error = null;

        var requested = Normalize(requestedProjectPath);
        if (requested is null)
        {
            effectiveProjectPath = _boundProjectPath;
            return true;
        }

        if (_boundProjectPath is null)
        {
            _boundProjectPath = requested;
            effectiveProjectPath = requested;
            return true;
        }

        if (string.Equals(_boundProjectPath, requested, StringComparison.OrdinalIgnoreCase))
        {
            effectiveProjectPath = _boundProjectPath;
            return true;
        }

        var boundProjectPath = _boundProjectPath ?? string.Empty;
        error = $"This MCP session is already bound to project '{boundProjectPath}' and cannot use '{requested}'. Start a new MCP session for a different TIA project.";
        return false;
    }

    private static string? Normalize(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return null;
        }

        return projectPath!.Trim();
    }
}
