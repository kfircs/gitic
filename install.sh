#!/usr/bin/env bash
# install.sh - Build and install the gitic .NET tool globally

set -euo pipefail

# Define colors for terminal output
RED='\033[0;31m'
GREEN='\033[0;32m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}=== Gitic CLI Installer ===${NC}"

# Get the project root directory where this script resides
PROJECT_ROOT="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
cd "$PROJECT_ROOT"

# Check if dotnet is installed
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}Error: .NET SDK is not installed or not in PATH.${NC}"
    echo "Please install the .NET 10 SDK and try again."
    exit 1
fi

echo "Building and packing Gitic..."
rm -rf src/nupkg/ src/bin/ src/obj/ nupkg/
dotnet pack -c Release

# Extract version from Gitic.csproj
VERSION=$(grep -m 1 "<Version>" src/Gitic.csproj | sed -E 's/.*<Version>(.*)<\/Version>.*/\1/' || echo "0.2.0")
echo -e "Detected Gitic version: ${GREEN}${VERSION}${NC}"

PACKAGE_PATH="$PROJECT_ROOT/src/nupkg"

echo "Uninstalling any existing global tool..."
dotnet tool uninstall -g gitic >/dev/null 2>&1 || true

echo "Installing gitic globally from local package..."
dotnet tool install -g gitic --add-source "$PACKAGE_PATH" --version "$VERSION"

echo -e "${GREEN}Installation complete! You can now run 'gitic' from your terminal.${NC}"
