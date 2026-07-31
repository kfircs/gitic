#!/usr/bin/env bash
# install-release.sh - High-speed, single-file installer for Gitic

set -e

REPO="your-username/gitic" # Change to your actual github org/repo
INSTALL_DIR="/usr/local/bin"
LOCAL_INSTALL_DIR="$HOME/.local/bin"

echo "============================================="
echo "🚀 Gitic Installer"
echo "============================================="

# Option A Check: Check if .NET Tool path is available
if command -v dotnet >/dev/null 2>&1; then
    echo "💡 Note: You have .NET installed. You can also install Gitic as a global tool:"
    echo "   dotnet tool install -g gitic"
    echo ""
fi

# Detect OS
OS="$(uname -s)"
ARCH="$(uname -m)"

case "$OS" in
    Linux)
        OS_NAME="linux"
        ;;
    Darwin)
        OS_NAME="osx"
        ;;
    *)
        echo "❌ Error: Unsupported Operating System: $OS"
        exit 1
        ;;
esac

# Detect Architecture
case "$ARCH" in
    x86_64)
        ARCH_NAME="x64"
        ;;
    arm64|aarch64)
        ARCH_NAME="arm64"
        ;;
    *)
        echo "❌ Error: Unsupported Architecture: $ARCH"
        exit 1
        ;;
esac

# Construct binary name
# Matches release.yml artifacts: gitic (linux-x64), gitic-osx-intel (osx-x64), gitic-osx-arm (osx-arm64)
if [ "$OS_NAME" = "linux" ]; then
    BINARY_NAME="gitic"
elif [ "$OS_NAME" = "osx" ] && [ "$ARCH_NAME" = "x64" ]; then
    BINARY_NAME="gitic-osx-intel"
elif [ "$OS_NAME" = "osx" ] && [ "$ARCH_NAME" = "arm64" ]; then
    BINARY_NAME="gitic-osx-arm"
fi

# Fetch latest release URL
echo "🔍 Finding latest release of Gitic..."
LATEST_RELEASE_URL=$(curl -s "https://api.github.com/repos/$REPO/releases/latest" | grep "browser_download_url" | grep "$BINARY_NAME" | cut -d '"' -f 4 || true)

if [ -z "$LATEST_RELEASE_URL" ]; then
    # Fallback to general release construction if GitHub API is throttled or repo is not yet populated
    LATEST_RELEASE_URL="https://github.com/your-username/gitic/releases/latest/download/$BINARY_NAME"
    echo "⚠️  Could not fetch release URL via API (perhaps repo is still private). Using fallback URL: $LATEST_RELEASE_URL"
fi

# Determine target directory
TARGET_DIR="$INSTALL_DIR"
if [ ! -w "$TARGET_DIR" ]; then
    echo "⚠️  $TARGET_DIR is not writable. Attempting installation in $LOCAL_INSTALL_DIR..."
    mkdir -p "$LOCAL_INSTALL_DIR"
    TARGET_DIR="$LOCAL_INSTALL_DIR"
    
    # Add to path check
    if [[ ":$PATH:" != *":$LOCAL_INSTALL_DIR:"* ]]; then
        echo "💡 Note: Please add $LOCAL_INSTALL_DIR to your PATH to run 'gitic' from anywhere."
    fi
fi

TEMP_FILE="/tmp/gitic-install"
echo "📥 Downloading Gitic from $LATEST_RELEASE_URL..."
if command -v curl >/dev/null 2>&1; then
    curl -SL -o "$TEMP_FILE" "$LATEST_RELEASE_URL"
elif command -v wget >/dev/null 2>&1; then
    wget -O "$TEMP_FILE" "$LATEST_RELEASE_URL"
else
    echo "❌ Error: Neither curl nor wget found. Please install one of them."
    exit 1
fi

echo "⚙️  Installing to $TARGET_DIR/gitic..."
mv "$TEMP_FILE" "$TARGET_DIR/gitic"
chmod +x "$TARGET_DIR/gitic"

echo "🎉 Gitic successfully installed! Run 'gitic' to start the dashboard."
