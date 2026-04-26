using System;
using System.IO;
using System.Linq;
using Siemens.Engineering;

namespace TiaMcpServer.OpennessWorker.Openness;

public class TiaPortalSession : IDisposable
{
    private TiaPortal? _tiaPortal;
    private bool _disposed;

    public Project? Project { get; private set; }

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
            throw new InvalidOperationException("No running TIA Portal V21 instance found. Please start TIA Portal before using the MCP server.");
        }

        _tiaPortal = processes.First().Attach();
        _tiaPortal.Notification += OnNotification;
        _tiaPortal.Confirmation += OnConfirmation;
        _tiaPortal.Disposed += OnDisposed;
        Project = _tiaPortal.Projects.FirstOrDefault();

        Console.Error.WriteLine("Connected to running TIA Portal instance.");
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

    private static void OnConfirmation(object? sender, ConfirmationEventArgs e)
    {
        e.Result = ConfirmationResult.Yes;
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
