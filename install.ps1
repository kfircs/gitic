# install.ps1 - Build and install the gitic .NET tool globally

$ErrorActionPreference = "Stop"

Write-Host "Building and packing Gitic..."
dotnet pack -c Release

$PackagePath = "./nupkg"
$Version = "0.1.0"

Write-Host "Uninstalling any existing global tool..."
dotnet tool uninstall -g gitic -ErrorAction SilentlyContinue

Write-Host "Installing gitic globally from local package..."
dotnet tool install -g gitic --add-source "$PackagePath" --version "$Version"

Write-Host "Installation complete! You can now run 'gitic' from your terminal."
