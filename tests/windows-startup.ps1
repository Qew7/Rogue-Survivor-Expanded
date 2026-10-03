param([Parameter(Mandatory = $true)][string]$Archive)

$ErrorActionPreference = 'Stop'
$gameDir = Join-Path $env:RUNNER_TEMP 'rogue-windows-startup'
Expand-Archive -Path $Archive -DestinationPath $gameDir -Force

$configPath = Join-Path $gameDir 'RogueSurvivor.exe.config'
if (-not (Test-Path $configPath)) { throw "Missing $configPath" }
$config = [xml](Get-Content $configPath -Raw)
$runtime = $config.SelectSingleNode('/configuration/startup/supportedRuntime')
if ($null -eq $runtime -or $runtime.GetAttribute('version') -ne 'v4.0') {
  throw "Windows package must request the .NET Framework 4.x runtime"
}

$game = Start-Process -FilePath (Join-Path $gameDir 'RogueSurvivor.exe') -WorkingDirectory $gameDir -PassThru
$log = Join-Path $gameDir 'Config/log.txt'
try {
  $deadline = (Get-Date).AddSeconds(60)
  while ((Get-Date) -lt $deadline) {
    if ($game.HasExited) { throw "Game exited during startup (code $($game.ExitCode))" }
    if ((Test-Path $log) -and (Select-String -Path $log -Pattern 'loading images done' -Quiet)) {
      Write-Host 'Windows game reached image loading successfully'
      return
    }
    Start-Sleep -Milliseconds 500
  }
  throw 'Game did not finish image loading within 60 seconds'
} finally {
  if (-not $game.HasExited) { Stop-Process -Id $game.Id -Force }
  if (Test-Path $log) { Get-Content $log | Select-Object -Last 40 }
}
