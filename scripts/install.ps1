# install.ps1 - Build and install the gitic .NET tool globally

$ErrorActionPreference = "Stop"

# Get the directory of this script, then the project root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Resolve-Path "$ScriptDir\.."

Push-Location $ProjectRoot

Write-Host "Building and packing Gitic..."
dotnet pack -c Release

$PackagePath = "$ProjectRoot/nupkg"
$Version = "0.1.0"

Write-Host "Uninstalling any existing global tool..."
dotnet tool uninstall -g gitic -ErrorAction SilentlyContinue

Write-Host "Installing gitic globally from local package..."
dotnet tool install -g gitic --add-source "$PackagePath" --version "$Version"

Pop-Location

Write-Host "Installation complete! You can now run 'gitic' from your terminal."
