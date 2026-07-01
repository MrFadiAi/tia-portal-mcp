<#
.SYNOPSIS
  Rebuild the TIA Portal MCP server AND refresh the Openness Business-access whitelist in one
  shot -- the canonical dev loop after editing the C# worker.

.DESCRIPTION
  The whitelist Entry TIA checks stores the worker's SHA-256 (FileHash). Rebuilding the worker
  changes its hash, so the OLD Entry no longer matches and the "Business access" prompt returns.
  This script closes that loop in one command:
    1. Build TiaMcpServer.sln (Debug) -> regenerates host + worker exes (new hashes).
    2. Verify the worker exe was produced and report its new SHA-256.
    3. Run tia-openness-whitelist.ps1 -> writes/refreshes a whitelist Entry with the NEW hash for
       every TiaMcpServer worker, under every installed TIA version.
  After this, TIA Portal attaches the rebuilt worker WITHOUT prompting.

  Run it after every code change to the worker. Self-elevates (step 3 writes HKLM).

.PARAMETER Config
  Build configuration (Debug/Release). Default Debug -- matches the path tia-agent launches from
  (TiaMcpServer\bin\Debug\net8.0\TiaMcpServer.dll).

.PARAMETER DryRun
  Build for real (to prove it compiles) but only PREVIEW the whitelist writes (no HKLM changes).
  Useful to sanity-check the build before committing to an elevated whitelist refresh.

.EXAMPLE
  .\build-and-approve.ps1            # build Debug + refresh whitelist (elevated)
  .\build-and-approve.ps1 -DryRun    # build + preview whitelist writes only
  .\build-and-approve.ps1 -Config Release
#>
param(
    [string]$Config = "Debug",
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'

# --- self-elevate once (step 3 writes HKLM) -------------------------------------------
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltinRole]::Administrator)) {
    Write-Host "Not elevated -- relaunching as administrator (UAC)..." -ForegroundColor Yellow
    $relaunchArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $PSCommandPath, "-Config", $Config)
    if ($DryRun) { $relaunchArgs += "-DryRun" }
    $p = Start-Process -FilePath "powershell.exe" -ArgumentList $relaunchArgs -Verb RunAs -PassThru -Wait
    exit $p.ExitCode
}

$repoRoot   = Split-Path -Parent $PSScriptRoot
$sln        = Join-Path $repoRoot "TiaMcpServer.sln"
$wlScript   = Join-Path $PSScriptRoot "tia-openness-whitelist.ps1"
$workerExe  = Join-Path $repoRoot "TiaMcpServer\bin\$Config\net8.0\openness-worker\TiaMcpServer.OpennessWorker.exe"

Write-Host "=== build-and-approve ===" -ForegroundColor Cyan
Write-Host "Repo  : $repoRoot"
Write-Host "Config: $Config"
if ($DryRun) { Write-Host "Mode  : DRY RUN (whitelist writes previewed, not applied)" -ForegroundColor Magenta }
Write-Host ""

# --- pre-flight: a running tia-agent/TiaMcpServer locks the output DLLs ----------------
# Its host DLLs sit in TiaMcpServer\bin\Debug\net8.0\, so 'dotnet build' fails mid-copy with
# MSB3027 "file is locked". Catch this up front with an actionable message instead.
$running = @(Get-Process -Name 'TiaMcpServer' -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Write-Host "ABORTING: TiaMcpServer is running (PID $($running.Id -join ', '))." -ForegroundColor Yellow
    Write-Host "Its output DLLs are locked, so the build would fail with 'file in use' errors." -ForegroundColor Yellow
    Write-Host "Stop tia-agent (and any TIA Portal worker it spawned), then re-run this script." -ForegroundColor Yellow
    exit 1
}

# --- 1. Build -------------------------------------------------------------------------
Write-Host "[1/3] Building $sln ($Config)..." -ForegroundColor Yellow
& dotnet build $sln -c $Config --nologo 2>&1 | Select-Object -Last 6
if ($LASTEXITCODE -ne 0) {
    Write-Host "BUILD FAILED (exit $LASTEXITCODE). Fix the errors, then re-run." -ForegroundColor Red
    exit 1
}

# --- 2. Verify the worker exe + report its new hash -----------------------------------
Write-Host ""
Write-Host "[2/3] Verifying rebuilt worker..." -ForegroundColor Yellow
if (-not (Test-Path $workerExe)) {
    Write-Host "Worker exe not found: $workerExe" -ForegroundColor Red
    Write-Host "The host build should copy it via the CopyOpennessWorker target -- check the build log." -ForegroundColor White
    exit 1
}
$sha = [System.Security.Cryptography.SHA256]::Create()
try {
    $hash = [Convert]::ToBase64String($sha.ComputeHash([System.IO.File]::ReadAllBytes($workerExe)))
} finally { $sha.Dispose() }
Write-Host "  OK: $workerExe" -ForegroundColor Green
Write-Host "  new SHA-256 (FileHash): $hash" -ForegroundColor DarkGray

# --- 3. Refresh the whitelist with the new hashes -------------------------------------
Write-Host ""
Write-Host "[3/3] Refreshing Openness whitelist (all installed TIA versions)..." -ForegroundColor Yellow
$wlArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $wlScript)
if ($DryRun) { $wlArgs += "-DryRun" }
& powershell.exe @wlArgs
$wlExit = $LASTEXITCODE
if ($wlExit -ne 0) {
    Write-Host "Whitelist step exited $wlExit." -ForegroundColor Red
    exit $wlExit
}

Write-Host ""
if ($DryRun) {
    Write-Host "Dry run complete. Re-run without -DryRun to apply the whitelist." -ForegroundColor Green
} else {
    Write-Host "Done. The rebuilt worker is approved -- TIA Portal will attach without prompting." -ForegroundColor Green
    Write-Host "Restart tia-agent (so it spawns the rebuilt worker) before connecting." -ForegroundColor Cyan
}
