#!/bin/bash
# install.sh - Build and install the gitic .NET tool globally

set -e

echo "Building and packing Gitic..."
dotnet pack -c Release

PACKAGE_PATH="./nupkg"
VERSION="0.1.0"

echo "Uninstalling any existing global tool..."
dotnet tool uninstall -g gitic || true

echo "Installing gitic globally from local package..."
dotnet tool install -g gitic --add-source "$PACKAGE_PATH" --version "$VERSION"

echo "Installation complete! You can now run 'gitic' from your terminal."
