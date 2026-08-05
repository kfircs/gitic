#!/bin/bash
# install.sh - Build and install the gitic .NET tool globally

set -e

# Get the directory of this script, then the project root
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$( cd "$SCRIPT_DIR/.." && pwd )"

cd "$PROJECT_ROOT"

echo "Building and packing Gitic..."
dotnet pack -c Release

PACKAGE_PATH="$PROJECT_ROOT/src/nupkg"
VERSION="0.2.0"

echo "Uninstalling any existing global tool..."
dotnet tool uninstall -g gitic || true

echo "Installing gitic globally from local package..."
dotnet tool install -g gitic --add-source "$PACKAGE_PATH" --version "$VERSION"

echo "Installation complete! You can now run 'gitic' from your terminal."
