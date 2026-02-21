param(
    [switch]$Build
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$tailadminRoot = Join-Path $repoRoot 'tailadmin-html-pro-2.0-main'
$blazorWwwroot = Join-Path $repoRoot 'FinanceManager\wwwroot'
$dest = Join-Path $blazorWwwroot 'tailadmin'

if (-not (Test-Path $tailadminRoot)) {
    throw "Tailadmin folder not found at: $tailadminRoot"
}

if ($Build) {
    Push-Location $tailadminRoot
    try {
        npm run build
    }
    finally {
        Pop-Location
    }
}

$buildDir = Join-Path $tailadminRoot 'build'
if (-not (Test-Path $buildDir)) {
    throw "Tailadmin build output not found at: $buildDir. Run with -Build to generate it."
}

New-Item -ItemType Directory -Force -Path $dest | Out-Null

Copy-Item -Force (Join-Path $buildDir 'style.css') (Join-Path $dest 'style.css')
Copy-Item -Force (Join-Path $buildDir 'bundle.js') (Join-Path $dest 'bundle.js')
Copy-Item -Force (Join-Path $buildDir 'favicon.ico') (Join-Path $dest 'favicon.ico')
Copy-Item -Recurse -Force (Join-Path $buildDir 'src') (Join-Path $dest 'src')

Write-Host "Synced Tailadmin assets to: $dest"