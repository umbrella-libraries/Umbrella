[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$MigrationName,

    [string]$RepoRoot,
    [string]$MigrationsProject,
    [string]$StartupProject,
    [string]$Context
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-EfRepoRoot {
    $gitRoot = git rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($gitRoot)) {
        return (Resolve-Path -Path $gitRoot.Trim()).Path
    }
    return $PWD.Path
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Resolve-EfRepoRoot
}
else {
    $RepoRoot = (Resolve-Path -Path $RepoRoot).Path
}

# --- Auto-detect migrations project ---
if ([string]::IsNullOrWhiteSpace($MigrationsProject)) {
    $candidates = Get-ChildItem -Path $RepoRoot -Recurse -Filter '*.csproj' |
        Where-Object { $_.FullName -notmatch '[\\/]obj[\\/]' -and $_.BaseName -match '\.Migrations$' }

    if ($candidates.Count -eq 0) {
        throw "No migrations project found. Expected a .csproj whose name ends with '.Migrations'. Specify -MigrationsProject explicitly."
    }
    if ($candidates.Count -gt 1) {
        $names = ($candidates | ForEach-Object { [System.IO.Path]::GetRelativePath($RepoRoot, $_.FullName) }) -join ', '
        throw "Multiple migrations projects found: $names. Specify -MigrationsProject explicitly."
    }
    $MigrationsProject = [System.IO.Path]::GetRelativePath($RepoRoot, $candidates[0].FullName)
}

# --- Auto-detect startup project ---
if ([string]::IsNullOrWhiteSpace($StartupProject)) {
    $candidates = Get-ChildItem -Path $RepoRoot -Recurse -Filter '*.csproj' |
        Where-Object { $_.FullName -notmatch '[\\/]obj[\\/]' } |
        Where-Object {
            $content = Get-Content -Path $_.FullName -Raw -ErrorAction SilentlyContinue
            $content -match 'Sdk\s*=\s*"Microsoft\.NET\.Sdk\.Web"' -and
            $content -match '<PackageReference\s[^>]*Include\s*=\s*"Microsoft\.EntityFrameworkCore\.Tools"'
        }

    if ($candidates.Count -eq 0) {
        throw "No startup project found. Expected a Microsoft.NET.Sdk.Web project with a Microsoft.EntityFrameworkCore.Tools PackageReference. Specify -StartupProject explicitly."
    }
    if ($candidates.Count -gt 1) {
        $names = ($candidates | ForEach-Object { [System.IO.Path]::GetRelativePath($RepoRoot, $_.FullName) }) -join ', '
        throw "Multiple startup project candidates: $names. Specify -StartupProject explicitly."
    }
    $StartupProject = [System.IO.Path]::GetRelativePath($RepoRoot, $candidates[0].FullName)
}

# --- Snapshot migration files before running ---
$migrationsProjectFullPath = Join-Path -Path $RepoRoot -ChildPath $MigrationsProject
$migrationsDir = Join-Path -Path (Split-Path -Parent $migrationsProjectFullPath) -ChildPath 'Migrations'
$filesBefore = @{}
if (Test-Path $migrationsDir) {
    Get-ChildItem -Path $migrationsDir -File -Recurse | ForEach-Object {
        $filesBefore[$_.FullName] = $_.Length
    }
}

# --- Auto-detect DbContext via EF CLI ---
if ([string]::IsNullOrWhiteSpace($Context)) {
    Write-Host 'Detecting DbContext...' -ForegroundColor Cyan

    Push-Location $RepoRoot
    try {
        $output = dotnet ef dbcontext list --project $MigrationsProject --startup-project $StartupProject --json 2>&1
    }
    finally {
        Pop-Location
    }

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet ef dbcontext list failed. Specify -Context explicitly.`nOutput:`n$($output -join [System.Environment]::NewLine)"
    }

    $inJson = $false
    $jsonLines = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $output) {
        $text = "$line"
        if ($text -match '^//Begin') { $inJson = $true; continue }
        if ($inJson -and $text -match '^//End') { break }
        if ($inJson) { $jsonLines.Add($text) }
    }

    if ($jsonLines.Count -eq 0) {
        throw "Could not parse output of 'dotnet ef dbcontext list'. Specify -Context explicitly."
    }

    $contexts = ($jsonLines -join "`n") | ConvertFrom-Json
    $contextNames = @($contexts | ForEach-Object { $_.name })

    if ($contextNames.Count -eq 0) {
        throw "No DbContext found in project. Specify -Context explicitly."
    }
    if ($contextNames.Count -gt 1) {
        throw "Multiple DbContexts found: $($contextNames -join ', '). Specify -Context explicitly."
    }
    $Context = $contextNames[0]
}

# --- Run migration ---
Write-Host ""
Write-Host "Adding EF Core migration" -ForegroundColor Cyan
Write-Host "  Name               : $MigrationName"
Write-Host "  Migrations project : $MigrationsProject"
Write-Host "  Startup project    : $StartupProject"
Write-Host "  Context            : $Context"
Write-Host ""

Push-Location $RepoRoot
try {
    dotnet ef migrations add $MigrationName `
        --project $MigrationsProject `
        --startup-project $StartupProject `
        --context $Context

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet ef migrations add failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

# --- Report generated and modified files ---
$filesAfter = @{}
if (Test-Path $migrationsDir) {
    Get-ChildItem -Path $migrationsDir -File -Recurse | ForEach-Object {
        $filesAfter[$_.FullName] = $_.Length
    }
}

$newFiles = @($filesAfter.Keys | Where-Object { -not $filesBefore.ContainsKey($_) } | Sort-Object)
$modifiedFiles = @($filesAfter.Keys |
    Where-Object { $filesBefore.ContainsKey($_) -and $filesBefore[$_] -ne $filesAfter[$_] } |
    Sort-Object)

Write-Host ""
Write-Host "Migration '$MigrationName' created successfully." -ForegroundColor Green
Write-Host ""

if ($newFiles.Count -gt 0) {
    Write-Host "New files:"
    foreach ($f in $newFiles) {
        Write-Host "  + $([System.IO.Path]::GetRelativePath($RepoRoot, $f) -replace '\\', '/')"
    }
}

if ($modifiedFiles.Count -gt 0) {
    Write-Host ""
    Write-Host "Modified files:"
    foreach ($f in $modifiedFiles) {
        Write-Host "  ~ $([System.IO.Path]::GetRelativePath($RepoRoot, $f) -replace '\\', '/')"
    }
}

# Warn if the migration appears to be empty
$migrationFile = $newFiles |
    Where-Object { [System.IO.Path]::GetFileName($_) -notmatch '\.Designer\.cs$' -and $_.EndsWith('.cs') } |
    Select-Object -First 1

if ($migrationFile) {
    $content = Get-Content -Path $migrationFile -Raw
    if ($content -notmatch 'migrationBuilder\.\w+\(') {
        Write-Host ""
        Write-Warning "The migration appears to be empty — no pending model changes were detected. Verify your entity changes are saved before committing this migration."
    }
}
