<#
.SYNOPSIS
  Builds wintty from the working tree and starts it, for daily use and for trying features as they land.
.DESCRIPTION
  Publishes a self-contained x64 build to artifacts/wintty-dev, then launches it. Use -NoBuild to start the
  last build, -Pull to fetch and fast-forward main first, and -Log to follow the log after launching.
  Run from anywhere: the script locates the repository from its own path.
.EXAMPLE
  pwsh scripts/run-dev.ps1 -Pull
#>
param(
  [switch] $Pull,
  [switch] $NoBuild,
  [switch] $Log
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'artifacts\wintty-dev'
$exe = Join-Path $out 'Wintty.App.exe'

Push-Location $root
try {
  if ($Pull) {
    git fetch origin
    if ($LASTEXITCODE -ne 0) { throw 'git fetch failed' }
    $branch = git rev-parse --abbrev-ref HEAD
    if ($branch -ne 'main') { Write-Warning "On branch '$branch', not main; skipping the pull." }
    else { git pull --ff-only; if ($LASTEXITCODE -ne 0) { throw 'git pull failed' } }
  }

  if (-not $NoBuild) {
    Get-Process Wintty.App -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | ForEach-Object {
      Write-Warning "Closing the running dev build (pid $($_.Id)) so it can be replaced."
      $_ | Stop-Process -Force
    }
    $sha = (git rev-parse --short HEAD)
    dotnet publish src/Wintty.App/Wintty.App.csproj -c Release -r win-x64 -p:Platform=x64 --self-contained `
      "-p:Version=0.0.0-dev+$sha" -o $out
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
  }

  if (-not (Test-Path $exe)) { throw "No build found at $exe; run without -NoBuild." }
  Start-Process $exe
  Write-Host "Started $exe"
}
finally {
  Pop-Location
}

if ($Log) {
  $logFile = Join-Path $env:LOCALAPPDATA 'wintty\wintty.log'
  Write-Host "Following $logFile (Ctrl+C to stop)"
  if (-not (Test-Path $logFile)) { New-Item -ItemType File -Force $logFile | Out-Null }
  Get-Content $logFile -Tail 20 -Wait
}
