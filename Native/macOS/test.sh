#!/bin/zsh
set -euo pipefail
cd "$(dirname "$0")"
mkdir -p Build/ModuleCache
xcrun swiftc -swift-version 5 -module-cache-path "$PWD/Build/ModuleCache" Sources/PolicyStore.swift Tests/main.swift -o Build/PolicyTests
Build/PolicyTests
node Tests/safari.test.cjs
