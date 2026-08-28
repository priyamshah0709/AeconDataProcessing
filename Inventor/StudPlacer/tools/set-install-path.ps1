<#
.SYNOPSIS
    Repoint the StudPlacer iLogic rules at a new install root.

.DESCRIPTION
    The absolute install path appears several times across ilogic\*.iLogicVb, in
    the AddVbFile directives and in the STUD_RULES_DIR / STUD_PART_PATH defaults.
    AddVbFile is a pre-compile directive so its argument must be a literal string
    -- it cannot read a parameter -- which is why the path is repeated rather
    than derived. This keeps every copy in step.

.PARAMETER Root
    The install root, i.e. the folder that CONTAINS vb\, rules\ and ilogic\.
    Passing the ilogic subfolder works too; it is stripped automatically.

.EXAMPLE
    .\tools\set-install-path.ps1 -Root "C:\Inventor Project\Inventor\StudPlacer"

.EXAMPLE
    .\tools\set-install-path.ps1 -Root "C:\Inventor Project\Inventor\StudPlacer\ilogic"
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Root
)

$ErrorActionPreference = 'Stop'
$iLogicDir = Join-Path $PSScriptRoot '..\ilogic'
$newRoot = $Root.Trim().Trim('"').TrimEnd('\', '/')

# Accept the folder the user was most likely looking at in Explorer.
$leaf = Split-Path $newRoot -Leaf
if ($leaf -in @('ilogic', 'vb', 'rules', 'parts', 'samples', 'tools', 'tests')) {
    $parent = Split-Path $newRoot -Parent
    Write-Host "note: `"$newRoot`""
    Write-Host "      looks like the $leaf\ subfolder; using its parent as the install root:"
    Write-Host "      `"$parent`""
    $newRoot = $parent
}

$files = Get-ChildItem -Path $iLogicDir -Filter *.iLogicVb -File | Sort-Object Name
if (-not $files) { throw "No .iLogicVb files found in $iLogicDir" }

$oldRoot = $null
foreach ($f in $files) {
    $m = [regex]::Match((Get-Content $f.FullName -Raw), 'AddVbFile\s+"(?<root>.*)\\vb\\StudRules\.vb"')
    if ($m.Success) { $oldRoot = $m.Groups['root'].Value; break }
}
if (-not $oldRoot) { throw "Could not find the AddVbFile anchor; has the rule header been edited?" }

if ($oldRoot -ceq $newRoot) {
    Write-Host "already set to `"$newRoot`" -- nothing to do."
    exit 0
}

$total = 0
foreach ($f in $files) {
    $text = Get-Content $f.FullName -Raw
    $n = ([regex]::Matches($text, [regex]::Escape($oldRoot))).Count
    if ($n -gt 0) {
        # Write UTF-8 without a BOM: the iLogic rule editor shows a stray
        # character at the top of the file otherwise.
        [System.IO.File]::WriteAllText($f.FullName, $text.Replace($oldRoot, $newRoot),
                                       (New-Object System.Text.UTF8Encoding($false)))
        $total += $n
    }
    Write-Host ("  {0}: {1} occurrence(s)" -f $f.Name, $n)
}

Write-Host ""
Write-Host "  from: `"$oldRoot`""
Write-Host "    to: `"$newRoot`""
Write-Host "  $total path(s) updated across $($files.Count) rule file(s)."
Write-Host ""

# Verify the layout actually exists, so a typo surfaces here rather than as an
# AddVbFile failure inside the rule editor.
Write-Host "Checking the expected layout:"
$missing = 0
foreach ($rel in @('vb\StudRules.vb', 'vb\StudArray.vb', 'vb\StudPlacer.vb',
                   'rules\global_constraints.csv', 'rules\table_1_6_walls.csv',
                   'rules\table_1_7_floors.csv', 'parts\Stud_19x157.ipt')) {
    $p = Join-Path $newRoot $rel
    if (Test-Path -LiteralPath $p) {
        Write-Host "  OK      $p"
    } else {
        Write-Host "  MISSING $p" -ForegroundColor Yellow
        $missing++
    }
}
if ($missing -gt 0) {
    Write-Host ""
    Write-Host "$missing item(s) not found. The rules will fail until they exist." -ForegroundColor Yellow
    Write-Host "parts\Stud_19x157.ipt is the one you create yourself -- see INVENTOR_SETUP.md step 2."
}
