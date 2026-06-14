#Requires -RunAsAdministrator
<#
.SYNOPSIS
  Pre-approve every TiaMcpServer worker in TIA Portal's Openness "Business access" whitelist so
  it attaches to TIA Portal WITHOUT the "Business access" prompt. No manual 'Yes to all' needed.

.DESCRIPTION
  TIA stores the Openness whitelist at:
    HKLM:\SOFTWARE\Siemens\Automation\Openness\<VERSION>\Whitelist\<EXE-FILENAME>\<Entry>\{Path,DateModified,FileHash}
  FileHash = SHA-256 of the exe (base64). At attach time TIA looks for an Entry whose Path matches
  the connecting exe AND whose FileHash matches the exe's CURRENT SHA-256; if found, no prompt.

  This script computes the correct hash for every TiaMcpServer worker/host exe in the repo and
  writes (or refreshes) an Entry under EVERY installed TIA version -- so the prompt is gone
  immediately, with no click in TIA at all.

  IMPORTANT: RE-RUN THIS SCRIPT AFTER EVERY WORKER REBUILD. A rebuilt binary has a new SHA-256,
  so its old Entry no longer matches and the prompt returns. The script refreshes the hash.

  This replaces the earlier "replicate after Yes-to-all" approach, which stored the wrong format
  (Path_N values instead of Entry subkeys) and never computed a hash, so TIA ignored it.

.PARAMETER RepoRoot
  Root of the tia-portal-mcp repo (to discover worker/host exe paths). Defaults to the script's
  parent directory.

.PARAMETER DryRun
  Show what would change without writing to the registry.
#>

param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot),
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$opennessRoot = 'HKLM:\SOFTWARE\Siemens\Automation\Openness'

function Get-ExeHash {
    param([string]$Path)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.IO.File]::ReadAllBytes($Path)
        return [Convert]::ToBase64String($sha.ComputeHash($bytes))
    }
    finally { $sha.Dispose() }
}

# --- 1. Discover worker/host exes (exclude obj/verify build intermediates) ---
$exes = @()
if (Test-Path $RepoRoot) {
    $exes = Get-ChildItem -Path $RepoRoot -Recurse -File -Filter '*.exe' -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match 'TiaMcpServer(\.OpennessWorker(\.(Legacy|V16))?)?\.exe$' -and $_.FullName -notmatch '[\\/](obj|verify)[\\/]' } |
        Select-Object -ExpandProperty FullName -Unique
}
if ($exes.Count -eq 0) {
    Write-Host "No TiaMcpServer worker/host exes found under $RepoRoot" -ForegroundColor Red
    Write-Host "Build the solution first (dotnet build), then re-run." -ForegroundColor White
    exit 1
}

Write-Host "=== TIA Openness whitelist: pre-approve workers ===" -ForegroundColor Cyan
Write-Host "Repo: $RepoRoot"
if ($DryRun) { Write-Host "*** DRY RUN -- nothing will be written ***" -ForegroundColor Magenta }
Write-Host "`nWorker/host exes ($($exes.Count)):" -ForegroundColor Yellow
$exes | ForEach-Object { Write-Host "  - $_" }

# --- 2. Target every installed TIA version (over-whitelisting across versions is harmless) ---
$versions = @()
if (Test-Path $opennessRoot) {
    $versions = Get-ChildItem $opennessRoot -ErrorAction SilentlyContinue |
        Where-Object { $_.PSChildName -match '^\d+\.\d+$' } |
        Select-Object -ExpandProperty PSChildName
}
if ($versions.Count -eq 0) {
    Write-Host "`nNo TIA Openness versions found under $opennessRoot" -ForegroundColor Red
    exit 1
}
Write-Host "`nCovering TIA versions: $($versions -join ', ')" -ForegroundColor Yellow

# --- 3. For each (version, exe), ensure an Entry exists with the correct Path + FileHash ---
$added = 0; $updated = 0; $ok = 0
foreach ($ver in $versions) {
    $wlRoot = Join-Path $opennessRoot "$ver\Whitelist"
    if (-not (Test-Path $wlRoot)) {
        if ($DryRun) { Write-Host "  [would create] $wlRoot" -ForegroundColor DarkGray }
        else { New-Item -Path $wlRoot -Force | Out-Null }
    }

    foreach ($exePath in $exes) {
        $exeName = [System.IO.Path]::GetFileName($exePath)
        $hash    = Get-ExeHash -Path $exePath
        $stamp   = (Get-Item -LiteralPath $exePath).LastWriteTime.ToString('yyyy/MM/dd HH:mm:ss.fff')
        $exeKey  = Join-Path $wlRoot $exeName
        if (-not (Test-Path $exeKey)) {
            if (-not $DryRun) { New-Item -Path $exeKey -Force | Out-Null }
        }

        # Existing Entry subkeys look like: Entry, Entry (1), Entry (2), ...
        $existing = @(Get-ChildItem $exeKey -ErrorAction SilentlyContinue |
            Where-Object { $_.PSChildName -match '^Entry( \(\d+\))?$' })

        $matched = $null
        foreach ($e in $existing) {
            $p = (Get-ItemProperty -LiteralPath $e.PSPath -Name Path -ErrorAction SilentlyContinue).Path
            if ($p -eq $exePath) { $matched = $e.PSPath; break }
        }

        if ($matched) {
            $curHash = (Get-ItemProperty -LiteralPath $matched -Name FileHash -ErrorAction SilentlyContinue).FileHash
            if ($curHash -eq $hash) {
                $ok++
            }
            else {
                if ($DryRun) {
                    Write-Host "  [would refresh] v$ver \$exeName  hash $curHash -> $hash" -ForegroundColor DarkGray
                }
                else {
                    Set-ItemProperty -LiteralPath $matched -Name 'Path'         -Value $exePath -Force
                    Set-ItemProperty -LiteralPath $matched -Name 'FileHash'     -Value $hash    -Force
                    Set-ItemProperty -LiteralPath $matched -Name 'DateModified' -Value $stamp   -Force
                    Write-Host "  ~ refreshed hash: v$ver \$exeName" -ForegroundColor Cyan
                }
                $updated++
            }
        }
        else {
            # No Entry for this path yet -> create the next one (Entry, then Entry (1), Entry (2), ...)
            $names = $existing | Select-Object -ExpandProperty PSChildName
            if ($names -notcontains 'Entry') { $newName = 'Entry' }
            else {
                $maxN = 0
                foreach ($n in $names) { if ($n -match '\((\d+)\)' -and ([int]$matches[1]) -gt $maxN) { $maxN = [int]$matches[1] } }
                $newName = 'Entry (' + ($maxN + 1) + ')'
            }
            $newKey = Join-Path $exeKey $newName
            if ($DryRun) {
                Write-Host "  [would add] v$ver \$exeName \$newName" -ForegroundColor DarkGray
                Write-Host "               Path=$exePath" -ForegroundColor DarkGray
                Write-Host "               FileHash=$hash" -ForegroundColor DarkGray
            }
            else {
                New-Item -Path $newKey -Force | Out-Null
                New-ItemProperty -LiteralPath $newKey -Name 'Path'         -Value $exePath -PropertyType String -Force | Out-Null
                New-ItemProperty -LiteralPath $newKey -Name 'DateModified' -Value $stamp   -PropertyType String -Force | Out-Null
                New-ItemProperty -LiteralPath $newKey -Name 'FileHash'     -Value $hash    -PropertyType String -Force | Out-Null
                Write-Host "  + approved: v$ver \$exeName" -ForegroundColor Green
                Write-Host "      $exePath" -ForegroundColor DarkGray
            }
            $added++
        }
    }
}

Write-Host "`nSummary: $added added, $updated refreshed, $ok already current." -ForegroundColor Green
Write-Host "Re-run this script after every worker rebuild (a new binary has a new hash)." -ForegroundColor Cyan
