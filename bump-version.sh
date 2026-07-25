#!/bin/bash
# bump-version.sh - Updates the version of the gitic tool across the project

set -e

if [ -z "$1" ]; then
    echo "Usage: ./bump-version.sh <new_version>"
    echo "Example: ./bump-version.sh 0.1.1"
    exit 1
fi

NEW_VERSION=$1
CURRENT_VERSION=$(grep -oPm1 "(?<=<Version>)[^<]+" Gitic.csproj)

if [ "$NEW_VERSION" == "$CURRENT_VERSION" ]; then
    echo "Version is already $CURRENT_VERSION. Nothing to do."
    exit 0
fi

echo "Bumping version from $CURRENT_VERSION to $NEW_VERSION..."

# Update Gitic.csproj
sed -i.bak "s/<Version>$CURRENT_VERSION<\/Version>/<Version>$NEW_VERSION<\/Version>/" Gitic.csproj
rm -f Gitic.csproj.bak

# Update install.sh
sed -i.bak "s/VERSION=\"$CURRENT_VERSION\"/VERSION=\"$NEW_VERSION\"/" install.sh
rm -f install.sh.bak

# Update install.ps1
sed -i.bak "s/\$Version = \"$CURRENT_VERSION\"/\$Version = \"$NEW_VERSION\"/" install.ps1
rm -f install.ps1.bak

echo "Successfully bumped version to $NEW_VERSION across all files."
