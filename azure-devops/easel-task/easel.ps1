[CmdletBinding()]
param()

# Inputs (from task.json)
$path    = Get-VstsInput -Name 'path' -Require
$format  = Get-VstsInput -Name 'format'
$failOn  = Get-VstsInput -Name 'failOn'
$output  = Get-VstsInput -Name 'output'
$version = Get-VstsInput -Name 'version'

$ErrorActionPreference = 'Stop'
$env:PATH = "$HOME/.dotnet/tools" + [IO.Path]::PathSeparator + $env:PATH

Write-Host "Installing pac + easel..."
dotnet tool install --global Microsoft.PowerApps.CLI.Tool 2>$null; if ($LASTEXITCODE -ne 0) { dotnet tool update --global Microsoft.PowerApps.CLI.Tool }

if ([string]::IsNullOrWhiteSpace($version)) {
  dotnet tool install --global EaselCli 2>$null; if ($LASTEXITCODE -ne 0) { dotnet tool update --global EaselCli }
} else {
  dotnet tool install --global EaselCli --version $version 2>$null; if ($LASTEXITCODE -ne 0) { dotnet tool update --global EaselCli --version $version }
}

Write-Host "Running easel lint..."
easel lint "$path" --format $format --fail-on $failOn --output "$output"
$code = $LASTEXITCODE

# Surface SARIF as a build artifact when produced.
if (($format -eq 'sarif') -and (Test-Path $output)) {
  Write-Host "##vso[artifact.upload containerfolder=easel;artifactname=easel-sarif]$output"
}

# exit 1 = findings over threshold -> fail the task; 2/3/4 = error -> fail.
if ($code -ne 0) {
  Write-Host "##vso[task.complete result=Failed;]easel exit $code"
  exit $code
}
