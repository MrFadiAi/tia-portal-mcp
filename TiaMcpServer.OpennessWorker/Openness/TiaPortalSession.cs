using System;
using System.IO;
using System.Linq;
using Siemens.Engineering;

namespace TiaMcpServer.OpennessWorker.Openness;

public class TiaPortalSession : IDisposable
{
    private readonly bool _allowTiaConfirmations;
    private readonly int? _tiaVersion;
    private TiaPortal? _tiaPortal;
    private bool _disposed;

    public TiaPortalSession(bool allowTiaConfirmations = false, int? tiaVersion = null)
    {
        _allowTiaConfirmations = allowTiaConfirmations;
        _tiaVersion = tiaVersion;
    }

    public Project? Project { get; internal set; }

    public TiaPortal? TiaPortal => _tiaPortal;

    public bool IsConnected => _tiaPortal != null;

    public void Connect()
    {
        ThrowIfDisposed();

        if (IsConnected)
        {
            return;
        }

        var processes = TiaPortal.GetProcesses();
        if (!processes.Any())
        {
            throw new InvalidOperationException($"No running TIA Portal {AssemblyResolver.DetectedVersion?.DisplayName ?? "TIA Portal"} instance found. Please start TIA Portal before using the MCP server.");
        }

        // Always iterate through all processes — try Attach() on each until one succeeds.
        // This handles both multi-version scenarios and cases where some processes
        // can't accept connections (e.g., background workers).
        foreach (var proc in processes)
        {
            try
            {
                _tiaPortal = proc.Attach();

                // Register confirmation handler IMMEDIATELY after Attach() returns,
                // before any other operations that might trigger confirmations.
                _tiaPortal.Confirmation += OnConfirmation;
                _tiaPortal.Notification += OnNotification;
                _tiaPortal.Disposed += OnDisposed;

                Console.Error.WriteLine($"Successfully attached to TIA Portal (requested V{_tiaVersion?.ToString() ?? "auto"}).");
                break;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Attach attempt failed (requested V{_tiaVersion?.ToString() ?? "auto"}): {ex.Message}");
                _tiaPortal = null;
            }
        }

        if (_tiaPortal is null)
        {
            throw new InvalidOperationException(
                $"Could not attach to TIA Portal V{_tiaVersion?.ToString() ?? "auto"}. " +
                $"Tried {processes.Count()} running process(es) but none could be reached. " +
                $"Ensure TIA Portal has a project open and Openness is enabled.");
        }
        Project = _tiaPortal.Projects.FirstOrDefault();

        Console.Error.WriteLine($"Connected to running TIA Portal instance (requested V{_tiaVersion?.ToString() ?? "auto"}).");
    }

    public void OpenProject(string projectPath)
    {
        ThrowIfDisposed();

        if (!IsConnected)
        {
            Connect();
        }

        if (!File.Exists(projectPath))
        {
            throw new FileNotFoundException("TIA Portal project file was not found.", projectPath);
        }

        Project = _tiaPortal!.Projects.Open(new FileInfo(projectPath));
    }

    public void EnsureConnected()
    {
        ThrowIfDisposed();

        if (!IsConnected)
        {
            Connect();
        }
    }

    private static void OnNotification(object? sender, NotificationEventArgs e)
    {
        Console.Error.WriteLine($"TIA Notification: {e.Text}");
    }

    private void OnConfirmation(object? sender, ConfirmationEventArgs e)
    {
        e.Result = _allowTiaConfirmations
            ? ConfirmationResult.Yes
            : ConfirmationResult.No;
    }

    private void OnDisposed(object? sender, EventArgs e)
    {
        Console.Error.WriteLine("Attached TIA Portal instance was disposed.");
        _tiaPortal = null;
        Project = null;
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (disposing && _tiaPortal != null)
        {
            _tiaPortal.Notification -= OnNotification;
            _tiaPortal.Confirmation -= OnConfirmation;
            _tiaPortal.Disposed -= OnDisposed;
        }

        Project = null;
        _tiaPortal?.Dispose();
        _tiaPortal = null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(TiaPortalSession));
        }
    }
}
