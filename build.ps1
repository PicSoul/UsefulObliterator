<#
    Build and optionally install UsefulObliterator for local testing.

        .\build.ps1              build, validate and package
        .\build.ps1 -Install     also install a development copy into the Gale client profile
        .\build.ps1 -RemoveDev   take the development copy out again

    Testing a change: switch the released mod off in Gale, run -Install, play. When the new
    version is published, run -RemoveDev and switch the released mod back on in Gale.
    The development copy lives in its own folder, PicSoul-UsefulObliterator-DEV, and never
    touches the folder Gale manages - see devcopy.ps1 for why that matters.

    Packaging targets Hexium / Thunderstore package format: a zip
    with manifest.json, icon.png (exactly 256x256) and README.md at its root.
#>

[CmdletBinding()]
param(
    [switch]$Install,
    [switch]$RemoveDev,
    [string]$Profile = "1.0 Release Client Mods"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

function Fail($msg) { Write-Host "FAIL: $msg" -ForegroundColor Red; exit 1 }
function Ok($msg)   { Write-Host "  ok   $msg" -ForegroundColor Green }

function PluginRoot {
    $path = "$env:APPDATA\com.kesomannen.gale\valheim\profiles\$Profile\BepInEx\plugins"
    if (-not (Test-Path $path)) { Fail "profile not found: $path" }
    return $path
}

function RequireValheimClosed {
    if (Get-Process -Name "valheim" -ErrorAction SilentlyContinue) {
        Fail "Valheim is running; close it first"
    }
}

. "$root\devcopy.ps1"
$devFolder = "PicSoul-UsefulObliterator-DEV"
$devDll = "UsefulObliterator.dll"

if ($RemoveDev) {
    Remove-DevCopy -ProfileName $Profile -FolderName $devFolder -DllName $devDll
    Write-Host "`ndone.`n" -ForegroundColor Cyan
    exit 0
}

# ---------------------------------------------------------------- version sync
$manifest = Get-Content "$root\manifest.json" -Raw | ConvertFrom-Json
$csproj = [xml](Get-Content "$root\UsefulObliterator.csproj")
$version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

Write-Host "`nUseful Obliterator $($manifest.version_number)" -ForegroundColor Cyan

if ($version -ne $manifest.version_number) {
    Fail "version mismatch: manifest.json says $($manifest.version_number), csproj says $version"
}

$pluginSource = Get-Content "$root\src\UsefulObliteratorPlugin.cs" -Raw
if ($pluginSource -notmatch 'PluginVersion\s*=\s*"([^"]+)"') { Fail "could not find PluginVersion in UsefulObliteratorPlugin.cs" }
if ($Matches[1] -ne $manifest.version_number) {
    Fail "version mismatch: manifest.json says $($manifest.version_number), UsefulObliteratorPlugin.cs says $($Matches[1])"
}
Ok "version $($manifest.version_number) matches in manifest.json, csproj and UsefulObliteratorPlugin.cs"

$changelog = Get-Content "$root\CHANGELOG.md" -Raw
if ($changelog -notmatch [regex]::Escape("## $($manifest.version_number)")) {
    Fail "CHANGELOG.md has no '## $($manifest.version_number)' section"
}
Ok "CHANGELOG.md documents this version"

Write-Host "`nbuilding..." -ForegroundColor Cyan
$log = & dotnet build "$root\UsefulObliterator.csproj" -c Release -v minimal --nologo 2>&1
if ($LASTEXITCODE -ne 0) { $log; Fail "build failed" }
Ok "compiled"

$dll = "$root\bin\Release\UsefulObliterator.dll"
if (-not (Test-Path $dll)) { Fail "expected output missing: $dll" }

# --------------------------------------------------------------------- stage
$stage = "$root\package"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null

Copy-Item $dll $stage
foreach ($f in @("manifest.json", "icon.png", "README.md", "CHANGELOG.md", "LICENSE", "THIRD-PARTY-NOTICES.md")) {
    if (Test-Path "$root\$f") { Copy-Item "$root\$f" $stage }
}
if (Test-Path "$root\icons") {
    Copy-Item "$root\icons" $stage -Recurse
    Ok "staged $( (Get-ChildItem "$root\icons" -File).Count ) high-res custom icons"
}

# ------------------------------------------------------------------ validate
Write-Host "`nvalidating against package rules..." -ForegroundColor Cyan

foreach ($required in @("manifest.json", "icon.png", "README.md")) {
    if (-not (Test-Path "$stage\$required")) { Fail "$required missing from package root" }
}
Ok "manifest.json, icon.png, README.md present at package root"

if ($manifest.name -notmatch '^[a-zA-Z0-9_]+$') { Fail "name '$($manifest.name)' has illegal characters (allowed: a-z A-Z 0-9 _)" }
if ($manifest.name.Length -gt 128)              { Fail "name exceeds 128 characters" }
Ok "name '$($manifest.name)' is valid"

if ($manifest.version_number -notmatch '^\d+\.\d+\.\d+$') { Fail "version_number must be Major.Minor.Patch" }
Ok "version_number is valid semver"

if ($manifest.description.Length -gt 250) { Fail "description is $($manifest.description.Length) chars (max 250)" }
Ok "description is $($manifest.description.Length)/250 chars"

if ($null -eq $manifest.website_url) { Fail "website_url must be present (use an empty string if unused)" }
Ok "website_url present"

foreach ($dep in $manifest.dependencies) {
    if ($dep -notmatch '^[a-zA-Z0-9_]+-[a-zA-Z0-9_]+-\d+\.\d+\.\d+$') {
        Fail "dependency '$dep' is not in {team}-{package}-{major.minor.patch} form"
    }
}
Ok "$($manifest.dependencies.Count) dependency string(s) well-formed"

Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile("$stage\icon.png")
$w = $img.Width; $h = $img.Height
$img.Dispose()
if ($w -ne 256 -or $h -ne 256) { Fail "icon.png is ${w}x${h}, must be exactly 256x256" }
Ok "icon.png is 256x256"

try { [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes("$stage\README.md")) | Out-Null }
catch { Fail "README.md is not valid UTF-8" }
Ok "README.md is UTF-8"

# ---------------------------------------------------------------------- zip
$zip = "$root\$($manifest.name)-$($manifest.version_number).zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
# Built entry by entry rather than with Compress-Archive: Windows PowerShell's Compress-Archive
# writes paths in subfolders with backslashes (icons\TrophyBoar.png), which the zip format does not
# allow and Hexium rejects as "an unsafe ZIP entry path".
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::Open($zip, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    Get-ChildItem $stage -Recurse -File | ForEach-Object {
        $entryName = $_.FullName.Substring($stage.Length + 1).Replace([char]92, [char]47)
        [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
            $archive, $_.FullName, $entryName, [System.IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
}
finally {
    $archive.Dispose()
}

$check = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $badEntries = @($check.Entries | Where-Object { $_.FullName.Contains([string][char]92) -or $_.FullName.StartsWith('/') -or $_.FullName.Contains('..') })
}
finally {
    $check.Dispose()
}
if ($badEntries.Count -gt 0) { Fail "zip has unsafe entry paths: $($badEntries[0].FullName)" }
Ok "zip entry paths use forward slashes"
Ok "packaged $(Split-Path $zip -Leaf) ($([math]::Round((Get-Item $zip).Length / 1KB, 1)) KB)"

if (-not $Install) { Write-Host "`ndone.`n" -ForegroundColor Cyan; exit 0 }

# The high-resolution icons are staged beside the DLL, so installing the stage brings them along,
# laid out exactly as a package install lays them out.
Install-DevCopy -ProfileName $Profile -FolderName $devFolder -DllName $devDll `
    -Stage $stage -Version $manifest.version_number

Write-Host "`ndone.`n" -ForegroundColor Cyan
