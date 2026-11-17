#!/bin/zsh
set -euo pipefail
cd "$(dirname "$0")"
app="$PWD/Build/FocusParkHelper.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources" "$PWD/Build/ModuleCache"
xcrun swiftc -swift-version 5 -O -module-cache-path "$PWD/Build/ModuleCache" -framework AppKit -framework SafariServices -framework Network -framework CryptoKit Sources/PolicyStore.swift Sources/Relay.swift Sources/main.swift -o "$app/Contents/MacOS/FocusParkHelper"
cp Info.plist "$app/Contents/Info.plist"
codesign --force --sign - "$app"
printf '%s\n' "$app"
