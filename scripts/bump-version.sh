#!/usr/bin/env bash
# bump-version.sh - Standardized C# project version bumper
# Usage: ./scripts/bump-version.sh [patch|minor|major|X.Y.Z]

set -euo pipefail

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

# Find the main .csproj file
CSPROJ_FILE=""
if [ -d "src" ]; then
    CSPROJ_FILE=$(find src -maxdepth 2 -name "*.csproj" | head -n 1)
fi
if [ -z "$CSPROJ_FILE" ]; then
    CSPROJ_FILE=$(find . -maxdepth 1 -name "*.csproj" | head -n 1)
fi

if [ -z "$CSPROJ_FILE" ]; then
    echo -e "${RED}Error: Could not find any .csproj file.${NC}" >&2
    exit 1
fi

echo -e "Found project file: ${GREEN}${CSPROJ_FILE}${NC}"

# Read current version
CURRENT_VERSION=$(grep -m 1 "<Version>" "$CSPROJ_FILE" | sed -E 's/.*<Version>(.*)<\/Version>.*/\1/' || true)

if [ -z "$CURRENT_VERSION" ]; then
    CURRENT_VERSION="0.1.0"
    echo -e "${YELLOW}Warning: No <Version> tag found. Defaulting to ${CURRENT_VERSION}.${NC}"
fi

echo -e "Current Version: ${GREEN}${CURRENT_VERSION}${NC}"

# Parse the bump type or explicit version
BUMP_TYPE="patch"
if [ $# -ge 1 ]; then
    BUMP_TYPE="$1"
fi

NEW_VERSION=""

if [[ "$BUMP_TYPE" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    NEW_VERSION="$BUMP_TYPE"
else
    # Parse semantic version numbers
    IFS='.' read -r major minor patch <<< "$CURRENT_VERSION"
    
    case "$BUMP_TYPE" in
        major)
            NEW_VERSION="$((major + 1)).0.0"
            ;;
        minor)
            NEW_VERSION="${major}.$((minor + 1)).0"
            ;;
        patch)
            NEW_VERSION="${major}.${minor}.$((patch + 1))"
            ;;
        *)
            echo -e "${RED}Error: Invalid argument '${BUMP_TYPE}'. Use major, minor, patch, or a specific version (e.g., 1.2.3).${NC}" >&2
            exit 1
            ;;
    esac
fi

if [ "$NEW_VERSION" = "$CURRENT_VERSION" ]; then
    echo -e "${YELLOW}Version is already ${NEW_VERSION}. Nothing to do.${NC}"
    exit 0
fi

echo -e "Bumping version from ${YELLOW}${CURRENT_VERSION}${NC} to ${GREEN}${NEW_VERSION}${NC}..."

# Update the .csproj file
sed -i.bak "s/<Version>$CURRENT_VERSION<\/Version>/<Version>$NEW_VERSION<\/Version>/" "$CSPROJ_FILE"
rm -f "${CSPROJ_FILE}.bak"

# If there is an install.ps1 in scripts/, update it too
INSTALL_PS1="scripts/install.ps1"
if [ -f "$INSTALL_PS1" ]; then
    sed -i.bak "s/\$Version = \"$CURRENT_VERSION\"/\$Version = \"$NEW_VERSION\"/" "$INSTALL_PS1"
    rm -f "${INSTALL_PS1}.bak"
fi

echo -e "${GREEN}Successfully bumped version to ${NEW_VERSION} in ${CSPROJ_FILE}!${NC}"
