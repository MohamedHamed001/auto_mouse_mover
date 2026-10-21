#!/usr/bin/env bash
# Build "Auto Mouse Mover.app" for one Mac architecture and zip it.
# Runs on macOS (codesign and ditto are macOS tools).
#
# Usage: scripts/package-macos.sh <osx-arm64|osx-x64> <version>
# Output: artifacts/AutoMouseMover-macos-<arm64|x64>.zip
set -euo pipefail

RID="$1"
VERSION="$2"
ARCH="${RID#osx-}"
OUT="artifacts/macos-$ARCH"
APP="$OUT/Auto Mouse Mover.app"

rm -rf "$OUT"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

# 1. Self-contained build (includes .NET and Avalonia's native libraries)
dotnet publish AutoMouseMover.Mac/AutoMouseMover.Mac.csproj -c Release -r "$RID" --self-contained \
    -p:UseAppHost=true -p:Version="$VERSION" -o "$OUT/publish"

# 2. App bundle layout: program in MacOS/, icon in Resources/, description in Info.plist
cp -R "$OUT/publish/." "$APP/Contents/MacOS/"
cp AutoMouseMover.Mac/Assets/app.icns "$APP/Contents/Resources/"
sed "s/{VERSION}/$VERSION/g" AutoMouseMover.Mac/Packaging/Info.plist > "$APP/Contents/Info.plist"
chmod +x "$APP/Contents/MacOS/AutoMouseMover.Mac"

# 3. Ad-hoc signature: required for apps to run on Apple Silicon (not a Developer ID)
codesign --force --deep -s - "$APP"

# 4. Zip, keeping the .app folder and its macOS attributes
ditto -c -k --keepParent "$APP" "artifacts/AutoMouseMover-macos-$ARCH.zip"
echo "Created artifacts/AutoMouseMover-macos-$ARCH.zip"
