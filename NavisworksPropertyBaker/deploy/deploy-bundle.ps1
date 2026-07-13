# Deploys the plugin as an Autodesk ApplicationPlugins *.bundle to the current
# user's profile - NO admin rights required, and reliably scanned by Navisworks
# 2025/2026 (unlike the per-version %APPDATA%\...\Plugins folder, which some
# locked-down/corporate setups do not scan).
#
# Bundle layout created:
#   %APPDATA%\Autodesk\ApplicationPlugins\NavisworksPropertyBaker.bundle\
#       PackageContents.xml
#       Contents\NavisworksPropertyBaker.dll (+ .pdb)
#
# Usage (from the NavisworksPropertyBaker folder, after building in Visual Studio):
#   powershell -ExecutionPolicy Bypass -File deploy\deploy-bundle.ps1 -Config Release

param(
    [string]$Config = "Release"
)

$ErrorActionPreference = "Stop"

$projectDir = Split-Path -Parent $PSScriptRoot
$dll = Join-Path $projectDir "bin\x64\$Config\NavisworksPropertyBaker.dll"
if (-not (Test-Path $dll)) { $dll = Join-Path $projectDir "bin\$Config\NavisworksPropertyBaker.dll" }
if (-not (Test-Path $dll)) {
    Write-Error "Build output not found. Build the solution first (looked under bin\x64\$Config and bin\$Config)."
}

$bundle   = Join-Path $env:APPDATA "Autodesk\ApplicationPlugins\NavisworksPropertyBaker.bundle"
$contents = Join-Path $bundle "Contents"
New-Item -ItemType Directory -Force -Path $contents | Out-Null

Copy-Item $dll $contents -Force
$pdb = [System.IO.Path]::ChangeExtension($dll, ".pdb")
if (Test-Path $pdb) { Copy-Item $pdb $contents -Force }

# PackageContents.xml lives next to this script; copy it into the bundle root.
Copy-Item (Join-Path $PSScriptRoot "PackageContents.xml") $bundle -Force

# Clear any "downloaded from the internet" block flag (common with OneDrive-synced sources).
Get-ChildItem -Recurse $bundle | Unblock-File -ErrorAction SilentlyContinue

Write-Host "Bundle deployed to $bundle"
Get-ChildItem -Recurse $bundle | Select-Object FullName
Write-Host ""
Write-Host "Fully close Navisworks Manage 2026 and reopen it; the AECON Property Baker"
Write-Host "button appears under the 'Tool Add-ins 1' ribbon tab."
