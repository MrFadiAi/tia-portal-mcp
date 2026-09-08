namespace TiaMcpServer.Diagnostics;

/// <summary>Outcome of a single doctor check (or of the whole report). <see cref="Info"/> marks
/// "nothing wrong, just context" (e.g. no TIA Portal installed / running) so a healthy machine
/// with a partial install is not reported as a problem.</summary>
public enum DiagnosticStatus
{
    Ok,
    Info,
    Warn,
    Fail
}
