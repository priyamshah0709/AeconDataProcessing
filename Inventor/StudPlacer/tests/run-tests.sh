#!/usr/bin/env bash
# Full verification of the StudPlacer engine.
#
#   1. lint the code-rule CSVs
#   2. compile-check the .iLogicVb rule files the way iLogic will
#   3. compile and RUN the engine regression suite
#
# Requires the .NET SDK (any version with VB support, 8.0+).
#   macOS:    brew install dotnet
#   Windows:  winget install Microsoft.DotNet.SDK.8
#   Linux:    https://dotnet.microsoft.com/download
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT="$(cd "$HERE/.." && pwd)"

# Homebrew installs dotnet outside the default PATH on macOS.
if ! command -v dotnet >/dev/null 2>&1 && [ -x /opt/homebrew/bin/dotnet ]; then
  export PATH="/opt/homebrew/bin:$PATH"
  export DOTNET_ROOT="/opt/homebrew/opt/dotnet/libexec"
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1

echo "==> 1/3  linting code-rule tables"
python3 "$ROOT/tools/check_rule_tables.py"

echo
echo "==> 2/3  compile-checking the iLogic rules"
python3 "$HERE/ilogic-syntax/wrap_rules.py"
dotnet build "$HERE/ilogic-syntax/ILogicSyntax.vbproj" -v q --nologo

echo
echo "==> 3/3  engine regression suite"
dotnet run --project "$HERE/StudPlacer.Tests.vbproj" -v q --nologo
