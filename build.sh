#!/usr/bin/env bash
# Local build of the C# targets: core tests -> Release -> dist/PlayersInfo_{cssharp,swiftly}_<version>.zip
# The Metamod plugin (repo root) builds with AMBuild against hl2sdk on Linux — see docs/install.md;
# CI (.github/workflows/build.yml) packages everything.
#
#   ./build.sh            # version from PlayersInfo.cpp GetVersion() — one number for all frameworks
#   ./build.sh 1.2.3      # explicit version
set -euo pipefail
cd "$(dirname "$0")"

DOTNET="${DOTNET:-dotnet}"
VERSION="${1:-$(grep -oE 'GetVersion\(\) *\{ *return "[^"]+"' PlayersInfo.cpp | grep -oE '"[^"]+"' | tr -d '"')}"
VERSION="${VERSION#v}"

"$DOTNET" test core/PlayersInfo.Core.Tests/PlayersInfo.Core.Tests.csproj -c Release --nologo
"$DOTNET" build cssharp/PlayersInfo.CSSharp.csproj -c Release --nologo -warnaserror "-p:Version=$VERSION"
"$DOTNET" build swiftly/PlayersInfo.Swiftly.csproj -c Release --nologo -warnaserror "-p:Version=$VERSION"

ls -la dist/
