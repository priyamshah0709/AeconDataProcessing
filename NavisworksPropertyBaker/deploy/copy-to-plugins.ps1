# Copies the built plugin DLL into the Navisworks Plugins folder.
# Navisworks only discovers a plugin when the DLL sits in a subfolder of Plugins
# with the SAME NAME as the DLL file: Plugins\NavisworksPropertyBaker\NavisworksPropertyBaker.dll
#
# Usage (from the NavisworksPropertyBaker folder, after building in Visual Studio):
#   powershell -ExecutionPolicy Bypass -File deploy\copy-to-plugins.ps1
#   powershell -ExecutionPolicy Bypass -File deploy\copy-to-plugins.ps1 -NavisVersion 2026 -Config Release

param(
    [string]$NavisVersion = "2026",
    [string]$Config = "Debug"
)

$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $PSScriptRoot
$dll = Join-Path $projectDir "bin\x64\$Config\NavisworksPropertyBaker.dll"
if (-not (Test-Path $dll)) {
    # SDK-style output path fallback
    $dll = Join-Path $projectDir "bin\$Config\NavisworksPropertyBaker.dll"
}
if (-not (Test-Path $dll)) {
    Write-Error "Build output not found. Build the solution first (looked under bin\x64\$Config and bin\$Config)."
}

$pluginDir = Join-Path $env:APPDATA "Autodesk Navisworks Manage $NavisVersion\Plugins\NavisworksPropertyBaker"
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item $dll $pluginDir -Force

$pdb = [System.IO.Path]::ChangeExtension($dll, ".pdb")
if (Test-Path $pdb) { Copy-Item $pdb $pluginDir -Force }

Write-Host "Deployed to $pluginDir"
Write-Host "Restart Navisworks Manage $NavisVersion; the button appears under Tool add-ins."
